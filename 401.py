import json
import os
import random
import re
import time
import uuid
from concurrent.futures import ThreadPoolExecutor, as_completed
from datetime import datetime, timedelta, timezone
from pathlib import Path
from urllib import error, parse, request
import os

P401 = re.compile(
    r'(^|\D)401(\D|$)|unauthorized|unauthenticated|token\s+expired|login\s+required|authentication\s+failed',
    re.I,
)
PQUOTA = re.compile(
    r'(^|\D)(402|403|429)(\D|$)|quota|insufficient\s*quota|resource\s*exhausted|rate\s*limit|too\s+many\s+requests|payment\s+required|billing|credit|额度|用完|超限|上限|usage_limit_reached',
    re.I,
)
PREFIX_RE = re.compile(r'^(\d{4}-\d{2}-\d{2}-\d{2}-\d{2})_(.+)$')
WINDOW_KEYS = (
    'rate_limit',
    'code_review_rate_limit',
    'primary_window',
    'secondary_window',
    'error',
)
AUTH_FILE_STATUS_METHODS = ('PATCH', 'PUT', 'POST')

BASE_URL = 'http://192.168.100.66:80'
MANAGEMENT_KEY = ''
TIMEOUT_SECONDS = 20
ENABLE_API_CALL_CHECK = True
API_CALL_URL = os.environ.get(
    'CLIPROXY_API_CALL_URL', 'https://chatgpt.com/backend-api/wham/usage'
)
API_CALL_METHOD = os.environ.get('CLIPROXY_API_CALL_METHOD', 'GET')
API_CALL_ACCOUNT_ID = os.environ.get(
    'CLIPROXY_API_CALL_ACCOUNT_ID', '141c5c10-0993-45c2-ad18-9a01ba2ab3e0'
)
API_CALL_USER_AGENT = os.environ.get(
    'CLIPROXY_API_CALL_USER_AGENT',
    'codex_cli_rs/0.76.0 (Debian 13.0.0; x86_64) WindowsTerminal',
)
API_CALL_BODY = os.environ.get('CLIPROXY_API_CALL_BODY', '')
API_CALL_PROVIDERS = os.environ.get(
    'CLIPROXY_API_CALL_PROVIDERS', 'codex,openai,chatgpt'
)
API_CALL_MAX_PER_RUN = min(
    9, int(os.environ.get('CLIPROXY_API_CALL_MAX_PER_RUN', '9') or '9')
)
API_CALL_SLEEP_MIN = float(os.environ.get('CLIPROXY_API_CALL_SLEEP_MIN', '5') or '5')
API_CALL_SLEEP_MAX = float(os.environ.get('CLIPROXY_API_CALL_SLEEP_MAX', '10') or '10')
API_CALL_HISTORY_DAYS = 7
DRY_RUN = False
INTERVAL_SECONDS = 60
RUN_ONCE = False


def getCurrentTime():
    return datetime.now().astimezone().strftime('%Y-%m-%d %H:%M:%S %z')


def runId():
    return datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ')


def parseCsvSet(value):
    if not value:
        return set()
    return {part.strip().lower() for part in str(value).split(',') if part.strip()}


def getCurrentTimezone():
    return datetime.now().astimezone().tzinfo or timezone.utc


def safeJsonLoads(value):
    try:
        return json.loads(value)
    except Exception:
        return None


def api(
    baseUrl,
    key,
    method,
    path,
    timeout=20,
    query=None,
    expectJson=True,
    body=None,
    extraHeaders=None,
):
    url = baseUrl.rstrip('/') + '/v0/management' + path
    if query:
        url += '?' + parse.urlencode(query)

    headers = {
        'Authorization': 'Bearer ' + key,
        'Accept': 'application/json',
        'User-Agent': 'cliproxyapi-cleaner/1.0',
    }
    if extraHeaders:
        headers.update(extraHeaders)

    data = None
    if body is not None:
        if isinstance(body, (dict, list)):
            data = json.dumps(body, ensure_ascii=False).encode('utf-8')
            headers.setdefault('Content-Type', 'application/json')
        elif isinstance(body, bytes):
            data = body
        else:
            data = str(body).encode('utf-8')
            headers.setdefault('Content-Type', 'application/json')

    req = request.Request(url, data=data, headers=headers, method=method.upper())
    try:
        with request.urlopen(req, timeout=timeout) as resp:
            raw = resp.read()
            code = resp.getcode()
    except error.HTTPError as exc:
        raw = exc.read()
        code = exc.code
    except error.URLError as exc:
        raise RuntimeError('请求管理 API 失败: %s' % exc)

    if expectJson:
        try:
            payload = json.loads(raw.decode('utf-8')) if raw else {}
        except Exception:
            payload = {'raw': raw.decode('utf-8', errors='replace')}
        return code, payload
    return code, raw


def uploadAuthFile(baseUrl, key, fileName, payload, timeout):
    url = baseUrl.rstrip('/') + '/v0/management/auth-files'
    boundary = '----OpenCodeBoundary%s' % uuid.uuid4().hex
    fileBody = json.dumps(payload, ensure_ascii=False).encode('utf-8')
    parts = [
        ('--' + boundary).encode('utf-8'),
        (
            'Content-Disposition: form-data; name="file"; filename="%s"' % fileName
        ).encode('utf-8'),
        b'Content-Type: application/json',
        b'',
        fileBody,
        ('--' + boundary + '--').encode('utf-8'),
        b'',
    ]
    body = b'\r\n'.join(parts)
    req = request.Request(
        url,
        data=body,
        headers={
            'Authorization': 'Bearer ' + key,
            'Accept': 'application/json',
            'Content-Type': 'multipart/form-data; boundary=' + boundary,
            'User-Agent': 'cliproxyapi-cleaner/1.0',
        },
        method='POST',
    )
    try:
        with request.urlopen(req, timeout=timeout) as resp:
            raw = resp.read()
            return resp.getcode(), raw.decode('utf-8', errors='replace')
    except error.HTTPError as exc:
        raw = exc.read()
        return exc.code, raw.decode('utf-8', errors='replace')


def extractErrorMessage(message):
    try:
        if isinstance(message, str) and message.strip().startswith('{'):
            payload = json.loads(message)
            errorObj = payload.get('error') if isinstance(payload, dict) else None
            if isinstance(errorObj, dict):
                return (
                    str(errorObj.get('type') or '').strip(),
                    str(errorObj.get('message') or '').strip(),
                )
            if isinstance(errorObj, str):
                return 'error', errorObj
    except Exception:
        pass
    return None, str(message or '')


def simplifyReason(reason):
    text = str(reason or '').strip()
    if not text:
        return ''
    if not text.startswith('{'):
        return text[:160]
    payload = safeJsonLoads(text)
    if not isinstance(payload, dict):
        return text[:160]
    errorObj = payload.get('error')
    if isinstance(errorObj, dict):
        errorType = str(errorObj.get('type') or '').strip()
        errorMessage = str(errorObj.get('message') or '').strip()
        return (errorType or errorMessage or text)[:160]
    if isinstance(errorObj, str):
        return errorObj[:160]
    return text[:160]


def parseDatetimeValue(value):
    if value in (None, ''):
        return None
    if isinstance(value, datetime):
        return (
            value.astimezone(getCurrentTimezone())
            if value.tzinfo
            else value.replace(tzinfo=getCurrentTimezone())
        )
    if isinstance(value, (int, float)):
        timestamp = float(value)
        if timestamp <= 0:
            return None
        return datetime.fromtimestamp(timestamp, tz=getCurrentTimezone())
    text = str(value).strip()
    if not text:
        return None
    if re.fullmatch(r'\d{10}(?:\.\d+)?', text) or re.fullmatch(r'\d{13}', text):
        numeric = float(text)
        if len(text.split('.')[0]) >= 13:
            numeric = numeric / 1000.0
        return datetime.fromtimestamp(numeric, tz=getCurrentTimezone())
    normalized = text.replace('Z', '+00:00')
    if '.' in normalized:
        prefix, suffix = normalized.split('.', 1)
        suffixMatch = re.match(r'^(\d+)([+-]\d\d:\d\d)?$', suffix)
        if suffixMatch:
            fraction = suffixMatch.group(1)[:6].ljust(6, '0')
            tzPart = suffixMatch.group(2) or ''
            normalized = prefix + '.' + fraction + tzPart
    try:
        parsed = datetime.fromisoformat(normalized)
    except Exception:
        return None
    if parsed.tzinfo is None:
        parsed = parsed.replace(tzinfo=getCurrentTimezone())
    return parsed.astimezone(getCurrentTimezone())


def formatPrefix(dtValue):
    return dtValue.astimezone(getCurrentTimezone()).strftime('%Y-%m-%d-%H-%M')


def parsePrefixDatetime(name):
    match = PREFIX_RE.match(str(name or '').strip())
    if not match:
        return None
    try:
        return datetime.strptime(match.group(1), '%Y-%m-%d-%H-%M').replace(
            tzinfo=getCurrentTimezone()
        )
    except Exception:
        return None


def stripPrefix(name):
    match = PREFIX_RE.match(str(name or '').strip())
    if match:
        return match.group(2)
    return str(name or '').strip()


def buildRenamedFileName(name, resetDt):
    baseName = stripPrefix(name)
    prefix = formatPrefix(resetDt)
    return prefix + '_' + baseName if baseName else prefix


def classify(item):
    status = str(item.get('status', '')).strip().lower()
    message = str(item.get('status_message', '') or '').strip()
    errorType, _errorMessage = extractErrorMessage(message)
    text = (status + '\n' + message).lower()

    if errorType == 'usage_limit_reached' or 'usage_limit_reached' in text:
        return 'quota_exhausted', message or status or 'usage_limit_reached'
    if PQUOTA.search(text):
        return 'quota_exhausted', message or status or 'quota'
    if bool(item.get('disabled', False)) or status == 'disabled':
        return 'disabled', message or status or 'disabled'
    if P401.search(text):
        return 'unauthorized', message or status or '401/unauthorized'
    if bool(item.get('unavailable', False)) or status == 'error':
        return 'unavailable', message or status or 'error'
    return 'available', message or status or 'active'


def normalizeApiCallBody(body):
    if body is None:
        return '', None
    if isinstance(body, str):
        text = body
        trimmed = text.strip()
        if not trimmed:
            return text, None
        try:
            return text, json.loads(trimmed)
        except Exception:
            return text, text
    try:
        return json.dumps(body, ensure_ascii=False), body
    except Exception:
        return str(body), body


def isLimitReachedWindow(value):
    if not isinstance(value, dict):
        return False
    if value.get('allowed') is False:
        return True
    if value.get('limit_reached') is True:
        return True
    return False


def findResetDatetime(value, source='root'):
    if isinstance(value, dict):
        directCandidates = []
        for key in (
            'resets_at',
            'resetsAt',
            'resets_time',
            'resetsTime',
            'next_retry_after',
            'retry_after',
            'nextRetryAfter',
        ):
            if key in value:
                dtValue = parseDatetimeValue(value.get(key))
                if dtValue is not None:
                    directCandidates.append((dtValue, '%s.%s' % (source, key)))
        if directCandidates:
            return sorted(directCandidates, key=lambda item: item[0])[0]
        for key in WINDOW_KEYS:
            nested = value.get(key)
            if isinstance(nested, (dict, list)):
                dtValue, nestedSource = findResetDatetime(
                    nested, '%s.%s' % (source, key)
                )
                if dtValue is not None:
                    return dtValue, nestedSource
        for key, nested in value.items():
            if not isinstance(nested, (dict, list)):
                continue
            dtValue, nestedSource = findResetDatetime(nested, '%s.%s' % (source, key))
            if dtValue is not None:
                return dtValue, nestedSource
    elif isinstance(value, list):
        for index, nested in enumerate(value):
            dtValue, nestedSource = findResetDatetime(
                nested, '%s[%s]' % (source, index)
            )
            if dtValue is not None:
                return dtValue, nestedSource
    return None, None


def extractResetDatetimeFromItem(item):
    itemPayload = item if isinstance(item, dict) else {}
    statusMessage = str(itemPayload.get('status_message') or '').strip()
    if statusMessage.startswith('{'):
        parsed = safeJsonLoads(statusMessage)
        if isinstance(parsed, dict):
            parsedDt, parsedSource = findResetDatetime(parsed, 'status_message')
            if parsedDt is not None:
                return parsedDt, parsedSource
    for key in (
        'resets_at',
        'resetsAt',
        'resets_time',
        'resetsTime',
        'next_retry_after',
        'retry_after',
        'nextRetryAfter',
    ):
        if key not in itemPayload:
            continue
        dtValue = parseDatetimeValue(itemPayload.get(key))
        if dtValue is not None:
            return dtValue, 'item.%s' % key
    directDt, directSource = findResetDatetime(itemPayload, 'item')
    if directDt is not None:
        return directDt, directSource
    return None, None


def classifyApiCallResponse(payload):
    nestedStatus = payload.get('status_code', payload.get('statusCode', 0))
    try:
        nestedStatus = int(nestedStatus)
    except Exception:
        nestedStatus = 0

    header = payload.get('header') or payload.get('headers') or {}
    bodyText, body = normalizeApiCallBody(payload.get('body'))
    resetDt, resetSource = (
        findResetDatetime(body, 'api_call.body')
        if isinstance(body, (dict, list))
        else (None, None)
    )

    try:
        headerText = json.dumps(header, ensure_ascii=False)
    except Exception:
        headerText = str(header)

    if isinstance(body, (dict, list)):
        try:
            bodySignal = json.dumps(body, ensure_ascii=False)
        except Exception:
            bodySignal = bodyText
    else:
        bodySignal = bodyText

    classification = None
    reason = bodySignal or (
        'api-call status_code=%s' % nestedStatus if nestedStatus else 'ok'
    )
    if nestedStatus in (402, 403, 429):
        classification = 'quota_exhausted'
    elif nestedStatus == 401:
        classification = 'unauthorized'
    elif isinstance(body, dict):
        errorObj = body.get('error')
        if isinstance(errorObj, dict):
            errorType = str(errorObj.get('type') or '').strip().lower()
            errorMessage = str(errorObj.get('message') or '').strip()
            errorText = (errorType + '\n' + errorMessage).lower()
            if errorType == 'usage_limit_reached' or PQUOTA.search(errorText):
                classification = 'quota_exhausted'
                reason = bodySignal or errorMessage or errorType
            elif P401.search(errorText):
                classification = 'unauthorized'
                reason = bodySignal or errorMessage or errorType
        if classification is None:
            rateLimit = body.get('rate_limit')
            codeReviewRateLimit = body.get('code_review_rate_limit')
            if isLimitReachedWindow(rateLimit) or isLimitReachedWindow(
                codeReviewRateLimit
            ):
                classification = 'quota_exhausted'
                reason = bodySignal or 'rate_limit_reached'

    if classification is None:
        fallbackText = ('%s\n%s\n%s' % (nestedStatus, headerText, bodySignal)).lower()
        if PQUOTA.search(fallbackText) and nestedStatus != 200:
            classification = 'quota_exhausted'
        elif P401.search(fallbackText):
            classification = 'unauthorized'

    return {
        'classification': classification,
        'reason': reason,
        'status_code': nestedStatus,
        'reset_dt': resetDt,
        'reset_source': resetSource,
        'body': body,
    }


def buildApiCallPayload(item):
    headers = {
        'Authorization': 'Bearer $TOKEN$',
        'Content-Type': 'application/json',
        'User-Agent': API_CALL_USER_AGENT,
    }
    if API_CALL_ACCOUNT_ID.strip():
        headers['Chatgpt-Account-Id'] = API_CALL_ACCOUNT_ID.strip()

    payload = {
        'authIndex': str(item.get('auth_index') or '').strip(),
        'method': API_CALL_METHOD.upper(),
        'url': API_CALL_URL.strip(),
        'header': headers,
    }
    if API_CALL_BODY:
        payload['data'] = API_CALL_BODY
    return payload


def apiCallItemKey(item):
    account = str(item.get('account') or item.get('email') or '').strip().lower()
    if account:
        return 'account:' + account
    authIndex = str(item.get('auth_index') or '').strip()
    if authIndex:
        return 'auth_index:' + authIndex
    return 'name:' + str(item.get('name') or item.get('id') or '').strip()


def runApiCallProbe(item):
    requestPayload = buildApiCallPayload(item)
    code, payload = api(
        BASE_URL,
        MANAGEMENT_KEY,
        'POST',
        '/api-call',
        TIMEOUT_SECONDS,
        expectJson=True,
        body=requestPayload,
    )
    if code != 200:
        raise RuntimeError('调用 /api-call 失败: HTTP %s %s' % (code, payload))
    probe = classifyApiCallResponse(payload)
    probe['request'] = requestPayload
    probe['response'] = payload
    return probe


def disableAuthFile(name, disabledValue):
    payload = {'name': name, 'disabled': bool(disabledValue)}
    attempts = []
    for method in AUTH_FILE_STATUS_METHODS:
        code, resp = api(
            BASE_URL,
            MANAGEMENT_KEY,
            method,
            '/auth-files/status',
            TIMEOUT_SECONDS,
            expectJson=True,
            body=payload,
        )
        attempts.append({'method': method, 'code': code, 'response': resp})
        if 200 <= code < 300:
            return attempts[-1]
        if code not in (404, 405, 501):
            break
    raise RuntimeError('更新 auth-files/status 失败: %s' % attempts)


def renameAuthFile(oldName, newName, preserveDisabled):
    if not oldName or not newName or oldName == newName:
        return {'renamed': False, 'skip': 'same_name'}
    code, raw = api(
        BASE_URL,
        MANAGEMENT_KEY,
        'GET',
        '/auth-files/download',
        TIMEOUT_SECONDS,
        {'name': oldName},
        False,
    )
    if code != 200:
        raise RuntimeError('下载 auth 文件失败: %s HTTP %s' % (oldName, code))
    try:
        payload = json.loads(raw.decode('utf-8'))
    except Exception as exc:
        raise RuntimeError('解析 auth 文件失败: %s' % exc)

    uploadCode, uploadBody = uploadAuthFile(
        BASE_URL, MANAGEMENT_KEY, newName, payload, TIMEOUT_SECONDS
    )
    if uploadCode != 200:
        raise RuntimeError(
            '上传重命名文件失败: %s HTTP %s %s'
            % (newName, uploadCode, uploadBody[:300])
        )

    deleteCode, deletePayload = api(
        BASE_URL,
        MANAGEMENT_KEY,
        'DELETE',
        '/auth-files',
        TIMEOUT_SECONDS,
        {'name': oldName},
        True,
    )
    if deleteCode != 200:
        raise RuntimeError(
            '删除旧文件失败: %s HTTP %s %s' % (oldName, deleteCode, deletePayload)
        )

    if preserveDisabled:
        disableAuthFile(newName, True)

    return {
        'renamed': True,
        'old_name': oldName,
        'new_name': newName,
    }


def getHistoryPath():
    return Path(os.path.join(os.path.dirname(__file__), 'api_call_history.json'))


def loadApiCallHistory():
    path = getHistoryPath()
    if not path.exists():
        return {}
    try:
        payload = json.loads(path.read_text(encoding='utf-8'))
    except Exception:
        return {}
    return payload if isinstance(payload, dict) else {}


def saveApiCallHistory(history):
    path = getHistoryPath()
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(history, ensure_ascii=False, indent=2), encoding='utf-8')


def buildHistoryProbe(entry):
    if not isinstance(entry, dict):
        return None
    cachedProbe = entry.get('probe')
    if not isinstance(cachedProbe, dict):
        return None
    probe = dict(cachedProbe)
    probe['reset_dt'] = parseDatetimeValue(probe.get('reset_at_local'))
    return probe


def getApiCallEligible(item, row, nowDt, history):
    if not ENABLE_API_CALL_CHECK:
        return False, 'api_call_disabled'
    authIndex = str(item.get('auth_index') or '').strip()
    if not authIndex:
        return False, 'missing_auth_index'
    provider = str(item.get('provider') or item.get('type') or '').strip().lower()
    providerSet = parseCsvSet(API_CALL_PROVIDERS)
    if providerSet and provider not in providerSet:
        return False, 'provider_filtered'
    if row.get('reset_dt') is not None:
        return False, 'reset_time_already_available'
    prefixDt = row.get('prefix_dt')
    if row.get('disabled') and prefixDt is not None and prefixDt > nowDt:
        return False, 'disabled_and_not_due'

    key = apiCallItemKey(item)
    entry = history.get(key)
    lastDt = (
        parseDatetimeValue((entry or {}).get('lastApiCallAt'))
        if isinstance(entry, dict)
        else None
    )
    if lastDt is not None and (nowDt - lastDt) < timedelta(days=API_CALL_HISTORY_DAYS):
        return False, 'use_cached_probe'
    return True, 'missing_reset_time'


def updateApiCallHistory(history, item, nowDt, probe=None):
    key = apiCallItemKey(item)
    entry = {
        'lastApiCallAt': nowDt.isoformat(),
        'account': str(item.get('account') or item.get('email') or '').strip(),
        'lastFileName': str(item.get('name') or item.get('id') or '').strip(),
        'authIndex': str(item.get('auth_index') or '').strip(),
        'provider': str(item.get('provider') or item.get('type') or '').strip(),
        'statusCode': None if probe is None else probe.get('status_code'),
        'classification': None if probe is None else probe.get('classification'),
    }
    if isinstance(probe, dict):
        entry['probe'] = {
            'classification': probe.get('classification'),
            'reason': probe.get('reason'),
            'status_code': probe.get('status_code'),
            'reset_source': probe.get('reset_source'),
            'reset_at_local': None
            if probe.get('reset_dt') is None
            else probe['reset_dt'].isoformat(),
        }
    history[key] = entry


def applyCachedHistoryProbe(row, historyEntry, counts):
    probe = buildHistoryProbe(historyEntry)
    if probe is None:
        return False
    row['api_call_probe'] = probe
    row['api_call_decision'] = 'use_cached_probe'
    counts['api-call使用本地缓存'] += 1
    if row.get('reset_dt') is None and probe.get('reset_dt') is not None:
        row['reset_dt'] = probe.get('reset_dt')
        row['reset_source'] = 'history.' + str(probe.get('reset_source') or 'cached')
        print(
            '[reset-time] 本地缓存命中 name=%s source=%s local=%s'
            % (
                row['name'],
                row['reset_source'],
                formatPrefix(row['reset_dt']),
            ),
            flush=True,
        )
    if probe.get('classification') == 'quota_exhausted':
        row['classification'] = 'quota_exhausted'
        row['reason'] = probe.get('reason') or row.get('reason')
    print(
        '[api-call] 使用本地缓存 name=%s auth_index=%s status_code=%s result=%s'
        % (
            row['name'],
            row['auth_index'],
            probe.get('status_code'),
            probe.get('classification') or 'ok',
        ),
        flush=True,
    )
    return True


def pickApiCallSleepSeconds():
    minValue = max(0.0, float(API_CALL_SLEEP_MIN))
    maxValue = max(minValue, float(API_CALL_SLEEP_MAX))
    return random.uniform(minValue, maxValue)


def buildInitialRows(files, counts):
    rows = []
    for item in files:
        counts['检查总数'] += 1
        kind, reason = classify(item)
        resetDt, resetSource = extractResetDatetimeFromItem(item)
        prefixDt = parsePrefixDatetime(item.get('name'))
        row = {
            'item': item,
            'name': str(item.get('name') or item.get('id') or '').strip(),
            'provider': str(item.get('provider') or item.get('type') or '').strip(),
            'auth_index': str(item.get('auth_index') or '').strip(),
            'status': item.get('status'),
            'status_message': item.get('status_message'),
            'disabled': bool(item.get('disabled', False))
            or str(item.get('status') or '').strip().lower() == 'disabled',
            'classification': kind,
            'reason': reason,
            'reset_dt': resetDt,
            'reset_source': resetSource,
            'prefix_dt': prefixDt,
            'api_call_probe': None,
        }
        if resetDt is not None:
            print(
                '[reset-time] 普通扫描命中 name=%s source=%s local=%s'
                % (
                    row['name'],
                    resetSource,
                    formatPrefix(resetDt),
                ),
                flush=True,
            )
        print(
            '[scan] name=%s provider=%s auth_index=%s status=%s disabled=%s classification=%s reason=%s'
            % (
                row['name'],
                row['provider'],
                row['auth_index'],
                row['status'],
                row['disabled'],
                row['classification'],
                simplifyReason(row['reason']),
            ),
            flush=True,
        )
        rows.append(row)
    return rows


def runApiCallBatches(rows, history, counts):
    nowDt = datetime.now().astimezone()
    candidates = []
    for row in rows:
        historyEntry = history.get(apiCallItemKey(row['item']))
        eligible, reason = getApiCallEligible(row['item'], row, nowDt, history)
        row['api_call_decision'] = reason
        if eligible:
            candidates.append(row)
            print(
                '[api-call] 触发 name=%s auth_index=%s reason=%s'
                % (
                    row['name'],
                    row['auth_index'],
                    reason,
                ),
                flush=True,
            )
        else:
            if reason == 'use_cached_probe' and applyCachedHistoryProbe(
                row, historyEntry, counts
            ):
                continue
            if reason == 'use_cached_probe':
                counts['api-call缓存缺失回退重查'] += 1
                row['api_call_decision'] = 'cached_probe_missing_fallback_query'
                candidates.append(row)
                print(
                    '[api-call] 本地缓存缺失，回退重查 name=%s auth_index=%s'
                    % (
                        row['name'],
                        row['auth_index'],
                    ),
                    flush=True,
                )
                continue
            print(
                '[api-call] 跳过 name=%s auth_index=%s reason=%s'
                % (
                    row['name'],
                    row['auth_index'],
                    reason,
                ),
                flush=True,
            )

    counts['api-call候选数'] = len(candidates)
    if not candidates:
        return

    batchSize = max(1, min(int(API_CALL_MAX_PER_RUN), 9))
    batchCount = (len(candidates) + batchSize - 1) // batchSize
    print(
        '[api-call] 共 %s 个候选账号，分 %s 批，每批最多 %s 个'
        % (
            len(candidates),
            batchCount,
            batchSize,
        ),
        flush=True,
    )

    for batchIndex in range(batchCount):
        batch = candidates[batchIndex * batchSize : (batchIndex + 1) * batchSize]
        print(
            '[api-call批次 %s/%s] 并发探测 %s 个账号'
            % (
                batchIndex + 1,
                batchCount,
                len(batch),
            ),
            flush=True,
        )
        with ThreadPoolExecutor(max_workers=len(batch)) as executor:
            futureMap = {}
            for row in batch:
                futureMap[executor.submit(runApiCallProbe, row['item'])] = row

            for future in as_completed(futureMap):
                row = futureMap[future]
                counts['api-call实际触发数'] += 1
                try:
                    probe = future.result()
                    row['api_call_probe'] = probe
                    counts['api-call成功数'] += 1
                    if probe.get('classification') == 'quota_exhausted':
                        counts['api-call发现配额耗尽'] += 1
                    if (
                        row.get('reset_dt') is None
                        and probe.get('reset_dt') is not None
                    ):
                        row['reset_dt'] = probe.get('reset_dt')
                        row['reset_source'] = probe.get('reset_source')
                        print(
                            '[reset-time] api-call命中 name=%s source=%s local=%s'
                            % (
                                row['name'],
                                row['reset_source'],
                                formatPrefix(row['reset_dt']),
                            ),
                            flush=True,
                        )
                    if probe.get('classification') == 'quota_exhausted':
                        row['classification'] = 'quota_exhausted'
                        row['reason'] = probe.get('reason') or row.get('reason')
                    print(
                        '  [api-call完成] name=%s auth_index=%s status_code=%s result=%s'
                        % (
                            row['name'],
                            row['auth_index'],
                            probe.get('status_code'),
                            probe.get('classification') or 'ok',
                        ),
                        flush=True,
                    )
                    updateApiCallHistory(
                        history, row['item'], datetime.now().astimezone(), probe
                    )
                except Exception as exc:
                    counts['api-call失败数'] += 1
                    row['api_call_probe'] = {'error': str(exc)}
                    print(
                        '  [api-call失败] name=%s auth_index=%s error=%s'
                        % (
                            row['name'],
                            row['auth_index'],
                            exc,
                        ),
                        flush=True,
                    )
                    updateApiCallHistory(
                        history,
                        row['item'],
                        datetime.now().astimezone(),
                        {'status_code': None, 'classification': 'error'},
                    )
                saveApiCallHistory(history)

        if batchIndex + 1 < batchCount:
            sleepSeconds = pickApiCallSleepSeconds()
            if sleepSeconds > 0:
                print(
                    '[api-call批次 %s/%s] 整批完成，等待 %.1f 秒后继续下一批'
                    % (
                        batchIndex + 1,
                        batchCount,
                        sleepSeconds,
                    ),
                    flush=True,
                )
                time.sleep(sleepSeconds)


def performRename(row, counts):
    resetDt = row.get('reset_dt')
    name = row.get('name')
    if not name or resetDt is None:
        return
    targetName = buildRenamedFileName(name, resetDt)
    if targetName == name:
        row['rename_result'] = 'skip_same_name'
        return

    counts['待重命名'] += 1
    row['rename_from'] = name
    row['rename_to'] = targetName
    print('[rename] from=%s to=%s' % (name, targetName), flush=True)
    if DRY_RUN:
        row['rename_result'] = 'dry_run_skip'
        row['name'] = targetName
        return

    try:
        renameResult = renameAuthFile(name, targetName, row.get('disabled', False))
        row['rename_result'] = (
            'renamed'
            if renameResult.get('renamed')
            else renameResult.get('skip') or 'skipped'
        )
        row['rename_response'] = renameResult
        if renameResult.get('renamed'):
            counts['已重命名'] += 1
            row['name'] = targetName
            row['item']['name'] = targetName
            row['item']['id'] = targetName
            print('  [rename完成] %s -> %s' % (name, targetName), flush=True)
    except Exception as exc:
        counts['重命名失败'] += 1
        row['rename_result'] = 'rename_failed'
        row['rename_error'] = str(exc)
        print('  [rename失败] name=%s error=%s' % (name, exc), flush=True)


def performStatusActions(row, counts, nowDt):
    name = row.get('name')
    kind = row.get('classification')
    prefixDt = parsePrefixDatetime(name)
    row['prefix_dt'] = prefixDt

    if kind == 'quota_exhausted':
        counts['配额耗尽'] += 1
        if row.get('disabled'):
            print(
                '[quota] 已是禁用状态 name=%s reason=%s'
                % (name, simplifyReason(row.get('reason'))),
                flush=True,
            )
            return
        print(
            '[disable] name=%s reason=%s' % (name, simplifyReason(row.get('reason'))),
            flush=True,
        )
        if DRY_RUN:
            row['disable_result'] = 'dry_run_skip'
            return
        try:
            resp = disableAuthFile(name, True)
            row['disable_result'] = 'disabled_true'
            row['disable_response'] = resp
            row['disabled'] = True
            counts['额度账号已禁用'] += 1
            print(
                '  [disable完成] method=%s HTTP %s' % (resp['method'], resp['code']),
                flush=True,
            )
        except Exception as exc:
            counts['禁用失败'] += 1
            row['disable_result'] = 'disable_failed'
            row['disable_error'] = str(exc)
            print('  [disable失败] name=%s error=%s' % (name, exc), flush=True)
        return

    if row.get('disabled'):
        counts['已禁用'] += 1
        if prefixDt is None:
            print(
                '[enable-check] 跳过 name=%s reason=missing_prefix' % name, flush=True
            )
            return
        if prefixDt > nowDt:
            print(
                '[enable-check] 未到时间 name=%s enable_at=%s'
                % (name, formatPrefix(prefixDt)),
                flush=True,
            )
            return
        counts['到期待启用'] += 1
        print(
            '[enable] name=%s due_prefix=%s' % (name, formatPrefix(prefixDt)),
            flush=True,
        )
        if DRY_RUN:
            row['enable_result'] = 'dry_run_skip'
            return
        try:
            resp = disableAuthFile(name, False)
            row['enable_result'] = 'disabled_false'
            row['enable_response'] = resp
            row['disabled'] = False
            counts['已启用'] += 1
            print(
                '  [enable完成] method=%s HTTP %s' % (resp['method'], resp['code']),
                flush=True,
            )
        except Exception as exc:
            counts['启用失败'] += 1
            row['enable_result'] = 'enable_failed'
            row['enable_error'] = str(exc)
            print('  [enable失败] name=%s error=%s' % (name, exc), flush=True)
        return

    if kind == 'available':
        counts['可用账号'] += 1
    elif kind == 'unavailable':
        counts['不可用'] += 1
    elif kind == 'unauthorized':
        counts['401账号'] += 1


def runCheck():
    print('[scan] 请求 /v0/management/auth-files', flush=True)
    code, payload = api(BASE_URL, MANAGEMENT_KEY, 'GET', '/auth-files', TIMEOUT_SECONDS)
    if code != 200:
        print('[错误] 获取 auth-files 失败: HTTP %s %s' % (code, payload), flush=True)
        return None

    files = payload.get('files') or []
    if not isinstance(files, list):
        print('[错误] auth-files 返回异常: %s' % payload, flush=True)
        return None

    print('[scan] auth-files 返回 HTTP %s files=%s' % (code, len(files)), flush=True)

    rid = runId()
    reportRoot = Path('./reports/cliproxyapi-auth-cleaner')
    reportRoot.mkdir(parents=True, exist_ok=True)
    counts = {
        '检查总数': 0,
        '可用账号': 0,
        '配额耗尽': 0,
        '已禁用': 0,
        '不可用': 0,
        '401账号': 0,
        '待重命名': 0,
        '已重命名': 0,
        '重命名失败': 0,
        '额度账号已禁用': 0,
        '禁用失败': 0,
        '到期待启用': 0,
        '已启用': 0,
        '启用失败': 0,
        'api-call候选数': 0,
        'api-call实际触发数': 0,
        'api-call使用本地缓存': 0,
        'api-call缓存缺失回退重查': 0,
        'api-call成功数': 0,
        'api-call失败数': 0,
        'api-call发现配额耗尽': 0,
    }

    nowDt = datetime.now().astimezone()
    rows = buildInitialRows([item for item in files if isinstance(item, dict)], counts)
    history = loadApiCallHistory()
    runApiCallBatches(rows, history, counts)

    results = []
    for row in rows:
        if row.get('reset_dt') is not None:
            performRename(row, counts)
        performStatusActions(row, counts, nowDt)
        result = {
            'name': row.get('name'),
            'provider': row.get('provider'),
            'auth_index': row.get('auth_index'),
            'status': row.get('status'),
            'disabled': row.get('disabled'),
            'classification': row.get('classification'),
            'reason': row.get('reason'),
            'reset_source': row.get('reset_source'),
            'resets_at_local': (
                None if row.get('reset_dt') is None else row['reset_dt'].isoformat()
            ),
            'api_call_decision': row.get('api_call_decision'),
            'api_call_probe': row.get('api_call_probe'),
            'rename_from': row.get('rename_from'),
            'rename_to': row.get('rename_to'),
            'rename_result': row.get('rename_result'),
            'rename_error': row.get('rename_error'),
            'disable_result': row.get('disable_result'),
            'disable_error': row.get('disable_error'),
            'enable_result': row.get('enable_result'),
            'enable_error': row.get('enable_error'),
        }
        results.append(result)

    report = {
        'run_id': rid,
        'base_url': BASE_URL,
        'dry_run': DRY_RUN,
        'api_call_enabled': ENABLE_API_CALL_CHECK,
        'api_call_history_days': API_CALL_HISTORY_DAYS,
        'results': results,
        'summary': counts,
    }
    reportPath = reportRoot / ('report-' + rid + '.json')
    reportPath.write_text(
        json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8'
    )

    print('\n' + '=' * 60, flush=True)
    print('【统计结果】', flush=True)
    print('=' * 60, flush=True)
    for key, value in counts.items():
        print('  %s: %s' % (key, value), flush=True)
    print('\n【报告文件】', flush=True)
    print('  %s' % reportPath, flush=True)
    print('=' * 60, flush=True)
    return counts


def main():
    if not MANAGEMENT_KEY.strip():
        print('缺少 MANAGEMENT_KEY，请先在脚本顶部填写', flush=True)
        return 2

    print('\n' + '=' * 60, flush=True)
    print('【CLIProxyAPI 配额状态维护工具】', flush=True)
    print('=' * 60, flush=True)
    print('  base_url=%s' % BASE_URL, flush=True)
    print('  dry_run=%s' % DRY_RUN, flush=True)
    print('  enable_api_call_check=%s' % ENABLE_API_CALL_CHECK, flush=True)
    print('  api_call_history_days=%s' % API_CALL_HISTORY_DAYS, flush=True)
    print('  interval_seconds=%s' % INTERVAL_SECONDS, flush=True)
    print('=' * 60 + '\n', flush=True)

    if RUN_ONCE:
        runCheck()
        return 0

    loopCount = 0
    try:
        while True:
            loopCount += 1
            print('\n【第 %s 次检测】%s' % (loopCount, getCurrentTime()), flush=True)
            try:
                runCheck()
            except Exception as exc:
                print('[错误] 检测过程中发生异常: %s' % exc, flush=True)
            print('\n[loop] 等待 %s 秒后继续' % INTERVAL_SECONDS, flush=True)
            break
    except KeyboardInterrupt:
        print('\n用户中断程序，共执行 %s 次检测' % loopCount, flush=True)
        return 0


if __name__ == '__main__':
    raise SystemExit(main())
