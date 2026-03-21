#!/usr/bin/env python3
"""ClearVision SFT 训练数据生成器（算子目录驱动）"""
from __future__ import annotations

import argparse
import json
import random
from collections import Counter, deque
from dataclasses import dataclass
from pathlib import Path
from typing import Any

SYSTEM_PROMPT_FOR_SFT = """你是 ClearVision 工业视觉检测平台的工作流生成专家。
根据用户描述，从算子库选择合适算子，确定连接关系和参数，生成检测工作流 JSON。

规则：
1. 从 ImageAcquisition 开始，以 ResultOutput 结束
2. 只使用已注册算子，不可创造
3. 连线遵守端口类型兼容性
4. 输出纯 JSON，不含 Markdown 包装

输出格式：
{
  \"explanation\": \"简要说明\",
  \"operators\": [{\"tempId\":\"op_1\",\"operatorType\":\"...\",\"displayName\":\"...\",\"parameters\":{}}],
  \"connections\": [{\"sourceTempId\":\"op_1\",\"sourcePortName\":\"...\",\"targetTempId\":\"op_2\",\"targetPortName\":\"...\"}],
  \"parametersNeedingReview\": {\"op_N\": [\"ParamName\"]}
}"""

NUMERIC_TYPES = {"Float", "Integer"}


@dataclass(frozen=True)
class PortMeta:
    name: str
    data_type: str
    is_required: bool = False


@dataclass(frozen=True)
class ParameterMeta:
    name: str
    data_type: str
    default_value: Any
    min_value: Any
    max_value: Any
    is_required: bool
    options: tuple[str, ...]


@dataclass(frozen=True)
class OperatorMeta:
    id: str
    display_name: str
    category: str
    input_ports: tuple[PortMeta, ...]
    output_ports: tuple[PortMeta, ...]
    parameters: tuple[ParameterMeta, ...]


@dataclass
class OperatorInstance:
    temp_id: str
    meta: OperatorMeta
    parameters: dict[str, str]


def _bool(v: Any) -> bool:
    if isinstance(v, bool):
        return v
    if isinstance(v, str):
        return v.strip().lower() == "true"
    return False


def _f(v: Any) -> float | None:
    try:
        if v in (None, ""):
            return None
        return float(v)
    except Exception:
        return None


def _s(v: Any) -> str:
    if isinstance(v, bool):
        return "true" if v else "false"
    if isinstance(v, int):
        return str(v)
    if isinstance(v, float):
        t = f"{v:.4f}".rstrip("0").rstrip(".")
        return t if t else "0"
    return str(v)


def load_catalog(path: Path) -> dict[str, OperatorMeta]:
    raw = json.loads(path.read_text(encoding="utf-8"))
    ops: dict[str, OperatorMeta] = {}
    for item in raw.get("operators", []):
        ins = tuple(
            PortMeta(p.get("name", ""), p.get("dataType", "Any"), _bool(p.get("isRequired")))
            for p in item.get("inputPorts", [])
        )
        outs = tuple(
            PortMeta(p.get("name", ""), p.get("dataType", "Any"), False)
            for p in item.get("outputPorts", [])
        )
        params = tuple(
            ParameterMeta(
                name=p.get("name", ""),
                data_type=(p.get("dataType") or "string").lower(),
                default_value=p.get("defaultValue"),
                min_value=p.get("min"),
                max_value=p.get("max"),
                is_required=bool(p.get("isRequired")),
                options=tuple((o.get("value", "") for o in (p.get("options") or []))),
            )
            for p in item.get("parameters", [])
        )
        op = OperatorMeta(
            id=item.get("id", ""),
            display_name=item.get("displayName", item.get("id", "")),
            category=item.get("category", "未分类"),
            input_ports=ins,
            output_ports=outs,
            parameters=params,
        )
        ops[op.id] = op
    return ops


def compatible(src: str, dst: str) -> bool:
    if src == dst or src == "Any" or dst == "Any":
        return True
    return src in NUMERIC_TYPES and dst in NUMERIC_TYPES


def build_graph(ops: dict[str, OperatorMeta]) -> tuple[dict[tuple[str, str], list[tuple[str, str, str, str]]], dict[str, list[str]]]:
    edge_map: dict[tuple[str, str], list[tuple[str, str, str, str]]] = {}
    adj: dict[str, list[str]] = {k: [] for k in ops}
    for sid, sop in ops.items():
        if not sop.output_ports:
            continue
        for tid, top in ops.items():
            if sid == tid or not top.input_ports:
                continue
            options: list[tuple[str, str, str, str]] = []
            for o in sop.output_ports:
                for i in top.input_ports:
                    if compatible(o.data_type, i.data_type):
                        options.append((o.name, i.name, o.data_type, i.data_type))
            if options:
                edge_map[(sid, tid)] = options
                adj[sid].append(tid)
    return edge_map, adj


def shortest_path(adj: dict[str, list[str]], start: str, goal: str, max_depth: int = 8) -> list[str] | None:
    if start == goal:
        return [start]
    q: deque[list[str]] = deque([[start]])
    vis = {start}
    while q:
        p = q.popleft()
        if len(p) - 1 >= max_depth:
            continue
        n = p[-1]
        for m in adj.get(n, []):
            if m in vis:
                continue
            np = p + [m]
            if m == goal:
                return np
            vis.add(m)
            q.append(np)
    return None


def weighted_choice(candidates: list[str], usage: Counter[str], rng: random.Random) -> str:
    ws = [1.0 / (usage[c] + 1.0) for c in candidates]
    return rng.choices(candidates, weights=ws, k=1)[0]


def _to_int(v: Any) -> int | None:
    try:
        if v in (None, ""):
            return None
        return int(float(v))
    except Exception:
        return None


def _clamp_int(v: int, lo: int, hi: int) -> int:
    return max(lo, min(v, hi))


def _clamp_float(v: float, lo: float, hi: float) -> float:
    return max(lo, min(v, hi))


def _has_token(name: str, tokens: tuple[str, ...]) -> bool:
    text = name.lower()
    return any(token in text for token in tokens)


def sample_file(name: str, rng: random.Random) -> str:
    n = name.lower()
    if "model" in n or "onnx" in n:
        return f"models/defect_{rng.randint(1,8)}.onnx"
    if "label" in n or "class" in n:
        return f"models/labels_{rng.randint(1,8)}.txt"
    if "template" in n:
        return f"templates/template_{rng.randint(1,12)}.png"
    if "calib" in n:
        return f"calibration/camera_{rng.randint(1,5)}.json"
    if "save" in n:
        return f"output/result_{rng.randint(1,999)}.json"
    return f"data/input_{rng.randint(1,30)}.png"


def sample_param(p: ParameterMeta, rng: random.Random) -> str:
    d = "" if p.default_value is None else str(p.default_value)
    n = p.name.lower()
    if p.data_type == "enum":
        if p.options:
            if d and d in p.options and rng.random() < 0.6:
                return d
            return rng.choice(list(p.options))
        return d or "Default"
    if p.data_type == "bool":
        b = d.strip().lower() == "true" if d else (rng.random() < 0.5)
        if rng.random() < 0.35:
            b = not b
        return "true" if b else "false"
    if p.data_type == "int":
        mn, mx, dv = _f(p.min_value), _f(p.max_value), _f(p.default_value)
        if mn is None and mx is None:
            if dv is None:
                if _has_token(n, ("port",)):
                    mn, mx = 1, 65535
                elif _has_token(n, ("width", "height", "size", "radius", "roiw", "roih", "kernel")):
                    mn, mx = 1, 1024
                elif _has_token(n, ("x", "y", "left", "top", "row", "col", "index", "count", "num")):
                    mn, mx = 0, 2048
                else:
                    mn, mx = 0, 100
            else:
                span = max(1, int(abs(dv) * 0.3) + 1)
                if abs(dv) < 1 and _has_token(n, ("x", "y", "width", "height", "size", "count", "num", "index")):
                    mn, mx = 0, max(10, int(span * 20))
                else:
                    mn, mx = dv - span, dv + span
        elif mn is None:
            mn = mx - 100
        elif mx is None:
            mx = mn + 100
        lo, hi = int(round(min(mn, mx))), int(round(max(mn, mx)))
        return str(rng.randint(lo, hi))
    if p.data_type == "double":
        mn, mx, dv = _f(p.min_value), _f(p.max_value), _f(p.default_value)
        if mn is None and mx is None:
            if _has_token(n, ("confidence", "iou", "ratio", "score")):
                v = rng.uniform(0.2, 0.95)
            elif _has_token(n, ("pixelsize", "scale")):
                v = rng.uniform(0.005, 5.0)
            elif _has_token(n, ("angle",)):
                v = rng.uniform(-180.0, 180.0)
            elif _has_token(n, ("x", "y", "offset", "translate")):
                v = rng.uniform(-200.0, 200.0)
            elif dv is not None:
                v = rng.uniform(dv - 1.0, dv + 1.0)
            else:
                v = rng.uniform(0.0, 10.0)
        else:
            if mn is None:
                mn = mx - 10.0
            if mx is None:
                mx = mn + 10.0
            v = rng.uniform(min(mn, mx), max(mn, mx))
        return _s(v)
    if p.data_type == "file":
        return sample_file(p.name, rng)
    if p.data_type == "camerabinding":
        return rng.choice(["cam_main", "cam_left", "cam_right", "cam_top"])
    if p.data_type == "string":
        if "ip" in n or "host" in n:
            return f"192.168.1.{rng.randint(10,240)}"
        if "url" in n:
            return f"http://127.0.0.1:{rng.choice([5000,8000,8080])}/api/vision"
        if "port" in n:
            return str(rng.choice([502, 1883, 9600, 12000]))
        if "topic" in n:
            return rng.choice(["vision/result", "vision/alarm", "vision/metrics"])
        if "table" in n:
            return rng.choice(["InspectionResults", "DefectLog", "MeasureLog"])
        if d:
            return d
        return f"{p.name}_value"
    return d or f"{p.name}_value"


def gen_params(op: OperatorMeta, rng: random.Random) -> dict[str, str]:
    return {p.name: sample_param(p, rng) for p in op.parameters}


def _set_if(params: dict[str, str], key: str, value: str) -> None:
    if key in params:
        params[key] = value


def _field_semantic(field_name: str) -> str:
    low = field_name.lower()
    if "count" in low or "num" in low or "defect" in low or "blob" in low:
        return "count"
    if "score" in low or "confidence" in low:
        return "score"
    if "width" in low or "distance" in low or "length" in low or "angle" in low or "time" in low or "elapsed" in low or low.endswith("ms"):
        return "measure"
    if "image" in low or "mask" in low:
        return "image"
    return "generic"


def align_workflow_parameters(insts: list[OperatorInstance], conns: list[dict[str, str]], rng: random.Random) -> None:
    by_id = {i.temp_id: i for i in insts}

    # 先做单算子参数归一化，避免明显不合理值。
    for inst in insts:
        p = inst.parameters
        oid = inst.meta.id

        if oid == "ImageAcquisition":
            src = p.get("sourceType", "file")
            if src == "file":
                _set_if(p, "filePath", sample_file("filePath", rng))
            else:
                _set_if(p, "cameraId", rng.choice(["cam_main", "cam_left", "cam_right", "cam_top"]))
                _set_if(p, "filePath", "")

        elif oid == "Thresholding":
            use_otsu = p.get("UseOtsu", "false").lower() == "true"
            _set_if(p, "MaxValue", "255")
            if use_otsu:
                _set_if(p, "Threshold", "0")
                if p.get("Type") in {"0", "1", "2", "3", "4"}:
                    _set_if(p, "Type", "8")
            else:
                tv = _to_int(p.get("Threshold"))
                tv = 127 if tv is None else _clamp_int(tv, 0, 255)
                if tv == 0:
                    tv = rng.randint(60, 180)
                _set_if(p, "Threshold", str(tv))
                if p.get("Type") in {"8", "16"}:
                    _set_if(p, "Type", "0")

        elif oid == "ColorMeasurement":
            _set_if(p, "RoiX", str(rng.randint(0, 320)))
            _set_if(p, "RoiY", str(rng.randint(0, 320)))
            _set_if(p, "RoiW", str(rng.randint(60, 640)))
            _set_if(p, "RoiH", str(rng.randint(60, 640)))
            if p.get("ColorSpace", "Lab") == "Lab":
                _set_if(p, "RefL", _s(rng.uniform(35.0, 80.0)))
                _set_if(p, "RefA", _s(rng.uniform(-20.0, 20.0)))
                _set_if(p, "RefB", _s(rng.uniform(-20.0, 20.0)))
            else:
                _set_if(p, "RefL", _s(rng.uniform(0.0, 180.0)))
                _set_if(p, "RefA", _s(rng.uniform(0.0, 255.0)))
                _set_if(p, "RefB", _s(rng.uniform(0.0, 255.0)))

        elif oid == "DeepLearning":
            iv = _to_int(p.get("InputSize"))
            iv = 640 if iv is None else _clamp_int(iv, 320, 1280)
            iv = int(round(iv / 32) * 32)
            _set_if(p, "InputSize", str(iv))
            _set_if(p, "Confidence", _s(_clamp_float(rng.uniform(0.4, 0.8), 0.0, 1.0)))
            if "ModelPath" in p and not p["ModelPath"].lower().endswith(".onnx"):
                p["ModelPath"] = sample_file("model", rng)
            if "LabelFile" in p and not p["LabelFile"].lower().endswith(".txt"):
                p["LabelFile"] = sample_file("label", rng)

        elif oid == "UnitConvert":
            _set_if(p, "FromUnit", "Pixel")
            _set_if(p, "ToUnit", "mm")
            _set_if(p, "Scale", _s(rng.uniform(0.005, 0.05)))
            _set_if(p, "UseCalibration", "true")

        elif oid == "CoordinateTransform":
            _set_if(p, "PixelSize", _s(rng.uniform(0.005, 0.05)))
            if "CalibrationFile" in p and not p["CalibrationFile"].lower().endswith(".json"):
                p["CalibrationFile"] = sample_file("calib", rng)

    # 再做跨节点联动对齐。
    for c in conns:
        src = by_id.get(c["sourceTempId"])
        dst = by_id.get(c["targetTempId"])
        if not src or not dst:
            continue

        src_oid = src.meta.id
        dst_oid = dst.meta.id
        src_port = c["sourcePortName"]
        dst_port = c["targetPortName"]

        if src_oid == "ImageResize" and dst_oid == "DeepLearning":
            dl_size = _to_int(dst.parameters.get("InputSize"))
            dl_size = 640 if dl_size is None else _clamp_int(dl_size, 320, 1280)
            dl_size = int(round(dl_size / 32) * 32)
            _set_if(dst.parameters, "InputSize", str(dl_size))
            _set_if(src.parameters, "UseScale", "false")
            _set_if(src.parameters, "ScaleFactor", "1")
            _set_if(src.parameters, "Width", str(dl_size))
            _set_if(src.parameters, "Height", str(dl_size))

        if dst_oid == "ConditionalBranch" and dst_port == "Value":
            sem = _field_semantic(src_port)
            _set_if(dst.parameters, "FieldName", src_port)
            if sem == "count":
                _set_if(dst.parameters, "Condition", "GreaterThan")
                _set_if(dst.parameters, "CompareValue", "0")
            elif sem == "score":
                _set_if(dst.parameters, "Condition", "GreaterThan")
                _set_if(dst.parameters, "CompareValue", "0.7")
            elif sem == "measure":
                _set_if(dst.parameters, "Condition", "LessThan")
                _set_if(dst.parameters, "CompareValue", "1.0")
            elif sem == "image":
                _set_if(dst.parameters, "Condition", "NotEqual")
                _set_if(dst.parameters, "CompareValue", "")

        if dst_oid == "ResultJudgment" and dst_port == "Value":
            sem = _field_semantic(src_port)
            _set_if(dst.parameters, "FieldName", src_port)
            _set_if(dst.parameters, "MinConfidence", "0.5")
            _set_if(dst.parameters, "OkOutputValue", "OK")
            _set_if(dst.parameters, "NgOutputValue", "NG")
            if sem == "count":
                _set_if(dst.parameters, "Condition", "LessOrEqual")
                _set_if(dst.parameters, "ExpectValue", "0")
            elif sem == "score":
                _set_if(dst.parameters, "Condition", "GreaterOrEqual")
                _set_if(dst.parameters, "ExpectValue", "0.7")
            elif sem == "measure":
                _set_if(dst.parameters, "Condition", "LessOrEqual")
                _set_if(dst.parameters, "ExpectValue", "1.0")
            elif sem == "image":
                _set_if(dst.parameters, "Condition", "Contains")
                _set_if(dst.parameters, "ExpectValue", "")
            else:
                _set_if(dst.parameters, "Condition", "NotEqual")
                _set_if(dst.parameters, "ExpectValue", "")

        if dst_oid in {"ModbusCommunication", "SiemensS7Communication", "MitsubishiMcCommunication", "OmronFinsCommunication", "TcpCommunication"}:
            if "Operation" in dst.parameters and dst_port == "Data":
                _set_if(dst.parameters, "Operation", "Write")
            if "FunctionCode" in dst.parameters and dst_port == "Data":
                _set_if(dst.parameters, "FunctionCode", "WriteSingle")
            if "WriteValue" in dst.parameters and dst_port == "Data":
                if src_port in {"False", "Ng", "NG"}:
                    _set_if(dst.parameters, "WriteValue", "0")
                elif src_port in {"True", "Ok", "OK", "IsOk"}:
                    _set_if(dst.parameters, "WriteValue", "1")
                else:
                    _set_if(dst.parameters, "WriteValue", "1")


def prioritize_control_signal_options(
    options: list[tuple[str, str, str, str]],
    dst_operator_id: str,
) -> list[tuple[str, str, str, str]]:
    if dst_operator_id not in {"ConditionalBranch", "ResultJudgment", "Comparator", "MathOperation", "Statistics"}:
        return options
    preferred_src_types = {"Float", "Integer", "Boolean", "String"}
    preferred = [opt for opt in options if opt[2] in preferred_src_types]
    return preferred if preferred else options


def pick_edge(options: list[tuple[str, str, str, str]], rng: random.Random) -> tuple[str, str]:
    def sc(o: tuple[str, str, str, str]) -> int:
        if o[2] == o[3]:
            return 3
        if o[3] == "Any":
            return 2
        if o[2] in NUMERIC_TYPES and o[3] in NUMERIC_TYPES:
            return 1
        return 0
    best = max(sc(o) for o in options)
    cands = [o for o in options if sc(o) == best]
    c = rng.choice(cands)
    return c[0], c[1]


def dedupe(path: list[str]) -> list[str]:
    if not path:
        return path
    out = [path[0]]
    for x in path[1:]:
        if x != out[-1]:
            out.append(x)
    return out


def build_target_path(target: str, adj: dict[str, list[str]], start: str, end: str) -> list[str]:
    if target == start:
        p = [start]
    else:
        p = shortest_path(adj, start, target) or [start, target]
    if target != end:
        t = shortest_path(adj, target, end)
        if t:
            p.extend(t[1:])
        elif p[-1] != end:
            p.append(end)
    return dedupe(p)


def build_random_path(adj: dict[str, list[str]], usage: Counter[str], rng: random.Random, start: str, end: str) -> list[str]:
    p = [start]
    cur = start
    for _ in range(rng.randint(2, 6)):
        cands = [x for x in adj.get(cur, []) if x not in {start, end}]
        if not cands:
            break
        nxt = weighted_choice(cands, usage, rng)
        p.append(nxt)
        cur = nxt
    t = shortest_path(adj, cur, end)
    if t:
        p.extend(t[1:])
    elif p[-1] != end:
        p.append(end)
    return dedupe(p)


def align_path_topology(path: list[str], adj: dict[str, list[str]]) -> list[str]:
    out = list(path)

    # DeepLearning 前优先插入 ImageResize，形成更常见的工业推理链路。
    i = 1
    while i < len(out):
        if out[i] == "DeepLearning":
            prev = out[i - 1]
            has_resize = prev == "ImageResize"
            can_insert = "ImageResize" in adj.get(prev, []) and "DeepLearning" in adj.get("ImageResize", [])
            if (not has_resize) and can_insert:
                out.insert(i, "ImageResize")
                i += 1
        i += 1

    return dedupe(out)


def build_workflow(path: list[str], ops: dict[str, OperatorMeta], edge_map: dict[tuple[str, str], list[tuple[str, str, str, str]]], rng: random.Random, target: str) -> tuple[dict[str, Any], list[str]]:
    insts = [OperatorInstance(f"op_{i+1}", ops[op_id], gen_params(ops[op_id], rng)) for i, op_id in enumerate(path)]
    conns: list[dict[str, str]] = []
    used = set()
    filled: set[tuple[str, str]] = set()

    for i in range(len(insts) - 1):
        a, b = insts[i], insts[i + 1]
        opts = edge_map.get((a.meta.id, b.meta.id), [])
        if not opts:
            continue
        opts = prioritize_control_signal_options(opts, b.meta.id)
        sp, tp = pick_edge(opts, rng)
        k = (a.temp_id, sp, b.temp_id, tp)
        if k in used:
            continue
        used.add(k)
        filled.add((b.temp_id, tp))
        conns.append({"sourceTempId": a.temp_id, "sourcePortName": sp, "targetTempId": b.temp_id, "targetPortName": tp})

    for bi, b in enumerate(insts):
        if bi == 0:
            continue
        for ip in b.meta.input_ports:
            if not ip.is_required or (b.temp_id, ip.name) in filled:
                continue
            ok = False
            for ai in range(bi - 1, -1, -1):
                a = insts[ai]
                for op in a.meta.output_ports:
                    if not compatible(op.data_type, ip.data_type):
                        continue
                    k = (a.temp_id, op.name, b.temp_id, ip.name)
                    if k in used:
                        continue
                    used.add(k)
                    filled.add((b.temp_id, ip.name))
                    conns.append({"sourceTempId": a.temp_id, "sourcePortName": op.name, "targetTempId": b.temp_id, "targetPortName": ip.name})
                    ok = True
                    break
                if ok:
                    break

    if insts and insts[-1].meta.id == "ResultOutput" and not any(c["targetTempId"] == insts[-1].temp_id for c in conns):
        ro = insts[-1]
        for ai in range(len(insts) - 2, -1, -1):
            a = insts[ai]
            linked = False
            for op in a.meta.output_ports:
                for ip in ro.meta.input_ports:
                    if not compatible(op.data_type, ip.data_type):
                        continue
                    k = (a.temp_id, op.name, ro.temp_id, ip.name)
                    if k in used:
                        continue
                    used.add(k)
                    conns.append({"sourceTempId": a.temp_id, "sourcePortName": op.name, "targetTempId": ro.temp_id, "targetPortName": ip.name})
                    linked = True
                    break
                if linked:
                    break
            if linked:
                break

    align_workflow_parameters(insts, conns, rng)

    review: dict[str, list[str]] = {}
    for inst in insts:
        names: list[str] = []
        for p in inst.meta.parameters:
            dv = "" if p.default_value is None else str(p.default_value).strip()
            if p.is_required and (p.data_type in {"file", "camerabinding"} or dv == ""):
                names.append(p.name)
        if inst.meta.id == target:
            tune = [p.name for p in inst.meta.parameters if p.data_type in {"int", "double", "enum"} and p.name not in names]
            rng.shuffle(tune)
            names.extend(tune[:2])
        if names:
            seen = set()
            out = []
            for n in names:
                if n in seen:
                    continue
                out.append(n)
                seen.add(n)
            if out:
                review[inst.temp_id] = out[:3]

    mids = [i.meta.display_name for i in insts if i.meta.id not in {"ImageAcquisition", "ResultOutput"}]
    explanation = "流程从图像采集开始，直接进入结果输出。" if not mids else f"流程从图像采集开始，依次经过{'、'.join(mids)}，最后在结果输出节点汇总。"

    workflow = {
        "explanation": explanation,
        "operators": [
            {
                "tempId": i.temp_id,
                "operatorType": i.meta.id,
                "displayName": i.meta.display_name,
                "parameters": i.parameters,
            }
            for i in insts
        ],
        "connections": conns,
        "parametersNeedingReview": review,
    }
    return workflow, [i.meta.id for i in insts]


AIRCON_MODELS = [
    "KFR-35GW",
    "KFR-72LW",
    "KFRd-26GW",
    "KFR-50LW",
    "KFR-120QW",
]

AIRCON_STATIONS = [
    "内机蒸发器装配工位",
    "外机冷凝器装配工位",
    "铜管焊接工位",
    "总装终检工位",
    "包装前终检工位",
    "压缩机贴标与追溯工位",
    "电控盒装配工位",
]

SHIFT_NAMES = ["白班", "中班", "夜班"]

QUALITY_TARGETS = [
    "漏检率<1%",
    "误检率<2%",
    "节拍<=1.2s/台",
    "检测结果可追溯到SN",
    "OK/NG分拣信号稳定",
]

CATEGORY_TASKS = {
    "AI检测": [
        "检测蒸发器翅片倒伏、压伤和脏污",
        "识别铜管焊点虚焊、漏焊和烧穿",
        "检测外机护网变形与磕碰",
    ],
    "匹配定位": [
        "定位压缩机铭牌区域，给下游OCR提供ROI",
        "定位面板Logo位置，校验装配偏移",
    ],
    "变量": [
        "记录每100台空调的NG累计数",
        "统计每班次缺陷计数并用于换班清零",
    ],
    "图像处理": [
        "对多视角图像做拼接后统一检测",
        "做极坐标展开检查环形部件完整性",
    ],
    "定位": [
        "定位铜管端口中心，判断装配位置偏差",
        "定位矩形孔位并检查安装方向",
    ],
    "拆分组合": [
        "把多路相机画面拼接后输出总图",
        "切分多工位图像并分别检测后汇总",
    ],
    "数据处理": [
        "过滤重复框并统计有效缺陷数",
        "把检测结果写入数据库用于质量追溯",
    ],
    "标定": [
        "做像素到毫米标定，支持尺寸判定",
        "加载标定矩阵，保证换线后快速复用",
    ],
    "检测": [
        "测量翅片间距是否超公差",
        "测量铜管孔距和角度偏差",
        "评估焊缝宽度是否在规格内",
    ],
    "流程控制": [
        "按产线节拍触发并在异常时走分支",
        "根据检测值输出OK/NG判定路径",
    ],
    "特征提取": [
        "提取边缘与轮廓用于缺陷筛选",
        "做亚像素边缘定位用于高精度测量",
    ],
    "识别": [
        "读取压缩机铭牌与SN码并回传MES",
        "识别机身二维码并绑定生产批次",
    ],
    "辅助": [
        "管理检测ROI，避免非检测区域干扰",
        "在流程中记录工艺说明便于维护",
    ],
    "通信": [
        "把OK/NG和缺陷码发送给PLC",
        "把检测摘要上报到MES接口",
    ],
    "逻辑工具": [
        "格式化结果文本用于报工",
        "触发器与计时统计用于节拍监控",
    ],
    "通用": [
        "做类型转换和逻辑判断保证流程稳定",
        "将关键数值整理为可判定结果",
    ],
    "采集": [
        "在空调装配线上采集工件图像",
    ],
    "输出": [
        "输出终检结果并归档到本地文件",
    ],
    "颜色处理": [
        "检测面板色差和喷涂一致性",
    ],
    "预处理": [
        "抑制反光并增强细节，便于缺陷检测",
    ],
}


def pick_task_for_category(category: str, display_name: str, rng: random.Random) -> str:
    tasks = CATEGORY_TASKS.get(category)
    if tasks:
        return rng.choice(tasks)
    return f"在产线中使用{display_name}完成质量检测与结果输出"


def build_prompt(ops: dict[str, OperatorMeta], path: list[str], target: str, rng: random.Random) -> str:
    t = ops[target]
    mids = [ops[x].display_name for x in path if x not in {"ImageAcquisition", "ResultOutput"}]
    hi = "、".join(mids[:2]) if mids else t.display_name

    model = rng.choice(AIRCON_MODELS)
    station = rng.choice(AIRCON_STATIONS)
    shift = rng.choice(SHIFT_NAMES)
    quality_target = rng.choice(QUALITY_TARGETS)
    task = pick_task_for_category(t.category, t.display_name, rng)

    # 以空调产线为主（约85%），保留少量通用工业语料增强泛化。
    if rng.random() < 0.85:
        tmpls = [
            f"我们在空调机型{model}的{station}，{shift}要做视觉质检。请用{t.display_name}为核心搭一个从图像采集到结果输出的流程，目标是{quality_target}。",
            f"空调产线当前节拍很紧，{station}需要一套可落地方案：{task}。请按算子库真实参数生成完整工作流 JSON，重点包含{hi}。",
            f"请帮我给空调总装做一个在线检测流程，机型是{model}，场景在{station}。核心算子用{t.display_name}，并确保最终可输出OK/NG结果，要求{quality_target}。",
            f"我在空调工厂做AI+视觉改造，{station}想实现“{task}”。请生成完整检测工作流，主算子是{t.display_name}，并考虑产线连续运行。",
        ]
        return rng.choice(tmpls)

    generic_tmpls = [
        f"我们在离散制造产线要做在线质检，需求是{task}。请用{t.display_name}生成从图像采集到结果输出的完整流程。",
        f"请基于实际算子库设计工业视觉流程，核心包含{hi}，主算子是{t.display_name}，并满足{quality_target}。",
    ]
    return rng.choice(generic_tmpls)


def to_entry(prompt: str, workflow: dict[str, Any]) -> dict[str, Any]:
    return {
        "messages": [
            {"role": "system", "content": SYSTEM_PROMPT_FOR_SFT},
            {"role": "user", "content": prompt},
            {"role": "assistant", "content": json.dumps(workflow, ensure_ascii=False, separators=(",", ":"))},
        ]
    }


def generate(ops: dict[str, OperatorMeta], count: int, seed: int) -> tuple[list[dict[str, Any]], Counter[str]]:
    rng = random.Random(seed)
    edge_map, adj = build_graph(ops)
    start, end = "ImageAcquisition", "ResultOutput"
    if start not in ops or end not in ops:
        raise ValueError("catalog 缺少 ImageAcquisition 或 ResultOutput")

    usage: Counter[str] = Counter()
    entries: list[dict[str, Any]] = []

    ids = list(ops.keys())
    rng.shuffle(ids)
    for target in ids:
        path = build_target_path(target, adj, start, end)
        path = align_path_topology(path, adj)
        wf, used = build_workflow(path, ops, edge_map, rng, target)
        entries.append(to_entry(build_prompt(ops, path, target, rng), wf))
        usage.update(used)

    while len(entries) < count:
        if rng.random() < 0.4:
            cands = [x for x in ops if x not in {start, end}]
            target = weighted_choice(cands, usage, rng)
            path = build_target_path(target, adj, start, end)
        else:
            path = build_random_path(adj, usage, rng, start, end)
            mids = [x for x in path if x not in {start, end}]
            target = rng.choice(mids) if mids else start
        path = align_path_topology(path, adj)
        wf, used = build_workflow(path, ops, edge_map, rng, target)
        entries.append(to_entry(build_prompt(ops, path, target, rng), wf))
        usage.update(used)

    rng.shuffle(entries)
    return entries, usage


def write_jsonl(entries: list[dict[str, Any]], output: Path) -> None:
    output.parent.mkdir(parents=True, exist_ok=True)
    with output.open("w", encoding="utf-8", newline="\n") as f:
        for e in entries:
            f.write(json.dumps(e, ensure_ascii=False))
            f.write("\n")


def main() -> None:
    root = Path(__file__).resolve().parents[2]
    parser = argparse.ArgumentParser(description="根据 catalog 生成 SFT 训练数据")
    parser.add_argument("--catalog", type=Path, default=root / "算子资料" / "算子目录.json")
    parser.add_argument("--output", type=Path, default=Path(__file__).resolve().with_name("clearvision_sft_data.jsonl"))
    parser.add_argument("--count", type=int, default=1200)
    parser.add_argument("--seed", type=int, default=20260303)
    args = parser.parse_args()

    ops = load_catalog(args.catalog)
    total = max(args.count, len(ops))
    entries, usage = generate(ops, total, args.seed)
    write_jsonl(entries, args.output)

    covered = len([k for k in ops if usage[k] > 0])
    print(f"Done. total={len(entries)}")
    print(f"Catalog={args.catalog}")
    print(f"Output={args.output}")
    print(f"Coverage={covered}/{len(ops)} operators")


if __name__ == "__main__":
    main()
