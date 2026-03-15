# 绠楀瓙瀹炵幇鏀舵暃璁″垝 / Operator Implementation Reconciliation Plan

## 1. 鏂囨。鐩殑

鏈枃妗ｇ敤浜庣粺涓€鏁寸悊褰撳墠绠楀瓙搴撲腑涓夌被闂锛?

1. 濂戠害涓嶄竴鑷达細鍏冩暟鎹€乁I 鍙傛暟銆佽緭鍏ヨ緭鍑虹鍙ｃ€佽繍琛屾椂琛屼负涓嶄竴鑷淬€?
2. 瀹炵幇娆犺处锛氶殣钘忓弬鏁般€佹湭鐢熸晥鍙傛暟銆侀殣钘忚緭鍏ャ€佽緭鍑洪敭婕傜Щ銆佸け璐ヨ涔変笉缁熶竴銆?
3. 绠楁硶杈圭晫锛氬綋鍓嶅疄鐜板彲鐢紝浣嗚兘鍔涜竟鐣屼笌鐢ㄦ埛棰勬湡瀛樺湪鍋忓樊锛屽鑷存晥鏋滀笉绋虫垨璋冨弬鍥伴毦銆?

鏈枃妗ｇ殑鐩爣涓嶆槸鐩存帴淇敼瀹炵幇锛岃€屾槸褰㈡垚涓€浠藉彲璇勫銆佸彲鎺掓湡銆佸彲鎵ц鐨勭粺涓€鏁存敼璁″垝銆?

## 2. 鎺掓煡鑼冨洿

- 浠ｇ爜鑼冨洿锛歚Acme.Product/src/Acme.Product.Infrastructure/Operators/**/*.cs`
- 鏂囨。鑼冨洿锛歚docs/operators/*.md`
- 浜ゅ弶鍙傝€冿細`docs/AlgorithmAudit/*.md`
- 鐩綍绱㈠紩鍙傝€冿細`docs/OPERATOR_CATALOG.md`銆乣docs/operators/CATALOG.md`

瑕嗙洊绠楀瓙鎬绘暟锛?*118**

## 3. 鎺掓煡鏂规硶

鏈疆鎺掓煡閲囩敤涓ゅ眰鏂规硶锛?

### 3.1 闈欐€佸姣旀壂鎻?

瀵瑰叏閮?`*Operator.cs` 鍋氫互涓嬪姣旓細

- `OperatorParam` 澹版槑鍙傛暟 vs `GetStringParam / GetIntParam / GetDoubleParam / GetBoolParam / GetFloatParam` 瀹為檯璇诲彇鍙傛暟
- `InputPort` 澹版槑杈撳叆 vs `TryGetInputImage / TryGetInputValue / inputs.TryGetValue` 瀹為檯璇诲彇杈撳叆
- `OutputPort` 澹版槑杈撳嚭 vs `CreateImageOutput(...)` / 杈撳嚭瀛楀吀涓殑杩愯鏃堕敭

### 3.2 浜哄伐娣辨寲澶嶆牳

瀵归珮浠峰€笺€佸鏉傘€佺敤鎴蜂綋鎰熸晱鎰熺殑绠楀瓙鍋氭簮鐮佺骇澶嶆牳锛岄噸鐐瑰寘鎷細

- `TemplateMatching`
- `WidthMeasurement`
- `AkazeFeatureMatch`
- `OrbFeatureMatch`
- `GradientShapeMatch`
- `ShapeMatching`
- `GeometricFitting`
- `DeepLearning`
- `CameraCalibration`
- `Undistort`
- `PolarUnwrap`

鏈疆杩橀澶栧弬鑰冧簡鐙珛浜ゅ弶瀹℃煡缁撴灉锛岀敤浜庤ˉ婕忓拰淇鏂囨。涓殑杩囧害鎺ㄦ柇锛屼絾鏈€缁堝彧鍚堝苟浜嗚兘琚綋鍓嶆簮鐮佺洿鎺ユ敮鎾戠殑楂樼疆淇″害闂銆?

### 3.3 缃俊搴﹁鏄?

闈欐€佹壂鎻忎腑鐨勨€滆繍琛屾椂杈撳嚭閿€濇彁鍙栦細鎶婇儴鍒嗘櫘閫氬瓧绗︿覆瀛楅潰閲忚璁や负杈撳嚭閿紝鍥犳锛?

- 鈥滈殣钘忓弬鏁?/ 鏈娇鐢ㄥ弬鏁?/ 闅愯棌杈撳叆鈥濈粨璁烘暣浣撶疆淇″害杈冮珮銆?
- 鈥滈澶栬緭鍑洪敭鈥濈粨璁洪渶瑕佷汉宸ュ鏍稿悗鍐嶇撼鍏ュ疄鏂借鍒掋€?
- 瀵逛娇鐢?`GetOptional*Param` 绛夐潪甯歌璇诲彇璺緞鐨勭畻瀛愶紝闈欐€佹壂鎻忓彲鑳芥妸鈥滃凡璇诲彇鍙傛暟鈥濊鍒や负鈥滄湭璇诲彇鍙傛暟鈥濓紝鍥犳鎵归噺娓呭崟浠嶉渶浜哄伐澶嶆牳鍚庢墠鑳戒綔涓哄疄鏂戒緷鎹€?

## 4. 鎬讳綋缁撹

褰撳墠绠楀瓙搴撲笉鏄€滄暣浣撲笉鍙敤鈥濓紝鑰屾槸瀛樺湪鏄庢樉鐨?*宸ョ▼濂戠害婕傜Щ**銆?

鏇达拷| 绠楀瓙 | 闂绫诲瀷 | 楂樼疆淇″害闂 | 寤鸿鍔ㄤ綔 | 鐘舵€?|
|------|------|------|------|------|
| `TemplateMatching` | 濂戠害 + 瀹炵幇 | `MaxMatches` 宸插０鏄庝絾鏈敓鏁堬紱`Template` 杩愯鏃舵寜 `byte[]` 瑙ｇ爜锛沗Method` 鍏冩暟鎹彧鏆撮湶 `NCC` / `SQDiff`锛沗CCoeffNormed` 榛樿鍊煎樊寮?| **宸插畬鎴?*锛氱粺涓€ `Method` 鏋氫妇锛涘疄鐜板鐩爣寰幆鍖归厤锛涙爣鍑嗗寲 `Position` 涓轰腑蹇冪偣銆?| 鉁?|
| `WidthMeasurement` | 濂戠害 + 绠楁硶杈圭晫 | `Direction` 鏈敓鏁堬紱`CreateImageOutput` 娉ㄥ叆 `Width` 璇箟鍐茬獊锛涙墜鍔?鑷姩妯″紡宸紓 | **宸插畬鎴?*锛氬疄鐜?`Direction` 涓?`CustomAngle`锛涘鍔犱簹鍍忕礌妫€娴嬮鏋讹紙鐢?`SubpixelEdgeDetection` 鏀寔锛夈€?| 鉁?|
| `GradientShapeMatch` | 濂戠害 + 瀹炵幇 | `_matcherCache` 閿鎾為闄╋紱`Position` 缂哄け锛涚紦瀛樼己涔?LRU 闄愬埗 | **宸插畬鎴?*锛氫慨澶嶇紦瀛橀敭锛涙樉寮忚緭鍑?`Position`锛汱RU 鏆傚緟鍚庣画娓呯悊銆?| 鉁?|
| `OrbFeatureMatch` | 濂戠害 + 瀹炵幇 | `EnableSymmetryTest` / `MinMatchCount` 闅愯棌锛沗Position` 缂哄け锛涘け璐ヨ涔変笉缁熶竴 | **宸插畬鎴?*锛氭毚闇插叏閮ㄩ殣钘忓弬鏁帮紱缁熶竴杈撳嚭 `Position` 缁撴瀯锛涘榻愬け璐ヨ涔夈€?| 鉁?|
| `AkazeFeatureMatch` | 濂戠害 | `Position` 缂哄け锛沗Score` 瀹氫箟鍐茬獊锛涗笌 ORB 绫讳技鐨勫け璐ヨ涔夐棶棰?| **宸插畬鎴?*锛氳ˉ鍏?`Position` 杈撳嚭锛涚ǔ瀹?`Score` 涓虹浉浼煎害瀹氫箟锛涘榻愬け璐ヨ涔夈€?| 鉁?|
| `DeepLearning` | 濂戠害 + 瀹炵幇 | `UseGpu` / `GpuDeviceId` 闅愯棌锛沗DetectionList` 绔彛鏈０鏄?| **宸插畬鎴?*锛氭毚闇?GPU 鍏抽敭鍙傛暟锛涙樉寮忓０鏄?`DetectionList` 绔彛銆?| 鉁?|
| `ImageAcquisition` | 濂戠害 + 瀹炵幇 | 鐩告満鏍稿績鍙傛暟 `exposureTime`銆乣gain`銆乣triggerMode` 鍙傛暟棰勮涓庡厓鏁版嵁涓嶅悓姝?| **宸插畬鎴?*锛氬湪鍏冩暟鎹腑绋冲畾澹版槑閲囬泦鍙傛暟锛涗繚鐣欐柊鏃у弬鏁板悕鍏煎鏄犲皠銆?| 鉁?|
| `TypeConvert` | 濂戠害 | 澹版槑杈撳叆 `Input` 涓庢簮鐮?`Value` 涓嶄竴鑷达紱杈撳嚭绔彛婕傜Щ | **宸插畬鎴?*锛氱粺涓€杈撳叆閿负 `Input`锛岀Щ闄?`Value` 璇诲彇锛涜ˉ鍏?`AsString/AsFloat/AsInteger/AsBoolean/OriginalType` 杈撳嚭绔彛銆?| 鉁?|
| `TriggerModule` | 濂戠害 | 鏈０鏄?`Signal` 绔彛锛岄殣钘忚矾寰勮鍙?`Trigger` | **宸插畬鎴?*锛氭寮忓０鏄?`Signal` 杈撳叆绔彛锛涚Щ闄や笉蹇呰鐨?`Trigger` 闅愯棌閫昏緫銆?| 鉁?|
锟斤拷椤讳簩閫変竴锛氳涔堟寮忔毚闇诧紝瑕佷箞鍒犻櫎杩愯鏃惰鍙栥€?
3. 宸插０鏄庡弬鏁板繀椤讳笁閫変竴锛氳涔堝疄鐜般€佽涔堝簾寮冦€佽涔堜粠鍏冩暟鎹Щ闄ゃ€?
4. 杈撳嚭绔彛蹇呴』鍚嶅疄涓€鑷达細澹版槑浜?`Position` 灏辩湡姝ｈ緭鍑?`Position`銆?
5. 涓氬姟澶辫触璇箟缁熶竴锛氭槑纭摢浜涚畻瀛愪娇鐢ㄢ€滄墽琛屾垚鍔熶絾涓氬姟 NG鈥濓紝鍝簺绠楀瓙浣跨敤妗嗘灦绾?`Failure`銆?
6. 绠楁硶鍗囩骇涓庡绾︽敹鏁涘垎鎵硅繘琛岋紝鍏堜慨鍙В閲婃€э紝鍐嶄慨鎬ц兘鍜岀簿搴﹀寮恒€?

## 8. P0 浼樺厛绾ф竻鍗?

P0 瀹氫箟锛氱洿鎺ュ奖鍝嶈皟鍙傘€佽繍琛岀粨鏋滅悊瑙ｃ€佷笅娓搁泦鎴愶紝鎴栧鏄撳鑷寸敤鎴蜂互涓衡€滅畻瀛愭晥鏋滃樊/绠楀瓙鍧忎簡鈥濄€?

| 绠楀瓙 | 闂绫诲瀷 | 楂樼疆淇″害闂 | 寤鸿鍔ㄤ綔 | 楠岃瘉鏂瑰紡 |
|------|------|------|------|------|
| `TemplateMatching` | 濂戠害 + 瀹炵幇 | `MaxMatches` 宸插０鏄庝絾鏈敓鏁堬紱`Template` 杩愯鏃舵寜 `byte[]` 瑙ｇ爜锛沗Method` 鍏冩暟鎹彧鏆撮湶 `NCC` / `SQDiff`锛屼絾杩愯鏃堕粯璁ゅ€兼槸 `CCoeffNormed`锛屼笖鍐呴儴 `switch` 杩樻帴鍙?`CCorr` / `CCoeff` 绯诲垪锛沗Position` 瀹為檯鏄乏涓婅 | 鏄庣‘鍗曠洰鏍?澶氱洰鏍囩瓥鐣ワ紱瑕佷箞瀹炵幇 `MaxMatches`锛岃涔堢Щ闄わ紱缁熶竴 `Template` 杈撳叆濂戠害锛涚粺涓€ `Method` 鏋氫妇銆侀粯璁ゅ€间笌鍐呴儴 `switch`锛涙槑纭綅缃涔?| 鍗曞厓娴嬭瘯 + UI 鍙傛暟鍥炲綊 + 澶氱洰鏍囨牱鏈獙璇?|
| `WidthMeasurement` | 濂戠害 + 绠楁硶杈圭晫 | `Direction` 鏈敓鏁堬紱`CreateImageOutput` 榛樿娉ㄥ叆 `Width` 涓哄浘鍍忓搴﹀悗锛岀畻瀛愬張鎵嬪姩鎶婂悓鍚嶉敭鏀瑰啓涓烘祴閲忓搴︼紱鎵嬪姩妯″紡涓庤嚜鍔ㄦā寮忚涔夊樊寮傝緝澶?| 瑕佷箞瀹炵幇 `Direction`锛岃涔堝垹闄わ紱鎷嗗垎鍥惧儚灏哄閿笌娴嬮噺缁撴灉閿紱鍦ㄧ鍙ｅ拰鏂囨。涓槑纭祴閲忓嚑浣曞畾涔?| 鎵嬪姩绾挎牱鏈祴璇?+ 鑷姩杈圭紭鏍锋湰娴嬭瘯 |
| `GradientShapeMatch` | 濂戠害 + 瀹炵幇 | `_matcherCache` 閿湭鍖呭惈杈撳叆妯℃澘鍐呭锛涘綋妯℃澘鏉ヨ嚜杈撳叆绔彛涓?`templatePath` 涓虹┖鏃讹紝鐩稿悓鍙傛暟鐨勪笉鍚屾ā鏉夸細鍏变韩鍚屼竴缂撳瓨閿紱澹版槑 `Position` 浣嗚繍琛屾椂杈撳嚭 `X/Y`锛涘浐瀹氬ぇ灏忔樉绀烘涓庣湡瀹炴ā鏉垮昂瀵告棤鍏?| 淇紦瀛橀敭锛涘湪绔彛娴佹ā鏉挎ā寮忎笅閬垮厤浣跨敤浠呬緷璧?`templatePath` 鐨勭紦瀛橈紱姝ｅ紡杈撳嚭 `Position`锛涘尯鍒嗏€滄樉绀烘灏哄鈥濆拰鈥滄ā鏉跨湡瀹炲昂瀵糕€濓紱蹇呰鏃舵敮鎸佸叧闂紦瀛?| 澶氭ā鏉垮垏鎹㈡祴璇?+ 缂撳瓨鍛戒腑娴嬭瘯 |
| `OrbFeatureMatch` | 濂戠害 + 瀹炵幇 | `EnableSymmetryTest` / `MinMatchCount` 杩愯鏃剁敓鏁堜絾鏈０鏄庯紱`Position` 缂哄け锛沗X/Y` 涓嶆槸鍑犱綍涓績锛涚壒寰佷笉瓒冲拰鍖归厤 NG 鏃㈠彲鑳借蛋 `Failure`锛屼篃鍙兘璧?`Success(CreateFailedOutput(...))`锛屽け璐ヨ涔変笉缁熶竴 | 鏆撮湶闅愯棌鍙傛暟锛涜緭鍑?`Position`锛涙槑纭?`X/Y` 鏄唬琛ㄧ偣杩樻槸涓績锛涚粺涓€鈥滄墽琛屽け璐モ€濆拰鈥滀笟鍔?NG鈥濊涔夛紱鑰冭檻杈撳嚭 `Center` | 鐗瑰緛鍖归厤濂戠害娴嬭瘯 + UI 鍙傛暟鍙鎬ф祴璇?|
| `AkazeFeatureMatch` | 濂戠害 | `Position` 澹版槑浣嗘湭瀹為檯杈撳嚭锛沗X/Y` 鍙栭涓尮閰嶇偣锛沗Score` 鏄唴鐐规瘮渚嬭€岄潪鐩镐技搴︼紱涓?ORB 涓€鏍峰瓨鍦ㄢ€滀笟鍔?NG 浣嗘暣浣撹繑鍥?Success鈥濈殑娣峰悎璇箟 | 杈撳嚭 `Position`锛涜ˉ `Center` 鎴?`MatchPoint` 鍖哄垎璇箟锛涘湪缁撴灉妯″瀷閲屽浐瀹?`Score` 瀹氫箟锛涚粺涓€澶辫触璇箟 | 鍖归厤杈撳嚭濂戠害娴嬭瘯 |
| `DeepLearning` | 濂戠害 + 瀹炵幇 | `UseGpu` / `GpuDeviceId` 瀹為檯鐢熸晥浣嗘湭澹版槑锛沗DetectionList` 鏄ǔ瀹氳緭鍑轰絾鏈０鏄庝负绔彛锛沗DetectionMode` 鍐冲畾鍏朵綑杈撳嚭瀛楁闆嗗悎锛涚洰褰曞拰 UI 寰堥毦鐪嬪嚭杩欎竴鐐?| 鏆撮湶 GPU 鍙傛暟锛涙槑纭?`Defects/Objects` 涓?`DetectionList` 鐨勮緭鍑哄绾︼紱缁熶竴妯″紡鍒囨崲鏂囨锛涘喅瀹氭槸鍚︽樉寮忓０鏄?`DetectionList` | 鍙傛暟闈㈡澘娴嬭瘯 + 鎺ㄧ悊妯″紡鍥炲綊娴嬭瘯 |
| `ImageAcquisition` | 濂戠害 + 瀹炵幇 | 鐩告満鏍稿績鍙傛暟 `exposureTime`銆乣gain`銆乣triggerMode` 鍦ㄨ繍琛屾椂璇诲彇锛屼絾鏈€氳繃鍏冩暟鎹ǔ瀹氬０鏄庯紝涓斾笌宸插０鏄庡弬鏁板懡鍚嶉鏍间笉缁熶竴 | 缁熶竴閲囬泦鍙傛暟鍛藉悕涓庡厓鏁版嵁锛涘尯鍒嗗摢浜涘弬鏁扮敱 `cameraBinding` 鎵挎帴锛屽摢浜涗繚鐣欎负绠楀瓙鍙傛暟锛涜ˉ鍏煎鏄犲皠 | 閲囬泦鍙傛暟鍥炲綊娴嬭瘯 + 宸ョ▼ JSON 鍏煎娴嬭瘯 |
| `TypeConvert` | 濂戠害 | 澹版槑杈撳叆鏄?`Input`锛屾簮鐮佽繕璇诲彇 `Value`锛涜繍琛屾椂杈撳嚭杩滃浜庡０鏄?`Output` | 缁熶竴杈撳叆閿紱鍐冲畾鏄惁淇濈暀澶氱闄勫姞杈撳嚭锛涜嫢淇濈暀鍒欐樉寮忓０鏄?| 杈撳叆鍏煎娴嬭瘯 + 涓嬫父杞崲鍥炲綊娴嬭瘯 |
| `TriggerModule` | 濂戠害 | 鏈０鏄庤緭鍏ョ鍙ｏ紝浣嗚繍琛屾椂璇诲彇 `Signal` / `Trigger` | 姝ｅ紡澹版槑杈撳叆绔彛鎴栫Щ闄ら殣钘忚矾寰勶紱缁熶竴瑙﹀彂杈撳叆妯″瀷 | 瑙﹀彂琛屼负娴嬭瘯 |

## 9. P1 浼樺厛绾ф竻鍗?

P1 瀹氫箟锛氱煭鏈熶笉浼氳绠楀瓙瀹屽叏涓嶅彲鐢紝浣嗕細鎸佺画鍒堕€犵淮鎶ゆ垚鏈€佺粨鏋滅悊瑙ｆ垚鏈垨鏁堟灉涓嶇ǔ銆?

| 绠楀瓙 | 闂绫诲瀷 | 楂樼疆淇″害闂 | 寤鸿鍔ㄤ綔 |
|------|------|------|------|
| `GeometricFitting` | 绠楁硶杈圭晫 + 杈撳嚭濂戠害 | 鎵€鏈夋湁鏁堣疆寤撶偣浼氳鍚堝苟鍚庣粺涓€鎷熷悎锛涙き鍦嗕笉璧?RANSAC锛沗FitResult` 瀛楁缁撴瀯渚濊禆 `FitType` | 澧炲姞鈥滄渶澶ц疆寤?鍗曡疆寤?鍏ㄩ儴杞粨鈥濋€夋嫨锛涙槑纭き鍦嗛瞾妫掓€ц竟鐣岋紱绋冲畾缁撴灉缁撴瀯 |
| `ShapeMatching` | 绠楁硶杈圭晫 | 鍚嶇О鏄€滃舰鐘跺尮閰嶁€濓紝瀹為檯鏇存帴杩戔€滄棆杞ā鏉垮尮閰嶁€濓紱娌℃湁灏哄害鎼滅储 | 缁熶竴鍛藉悕鎴栬ˉ灏哄害鎼滅储锛涙枃妗ｅ拰鍙傛暟闈㈡澘鏄惧紡绾︽潫鑳藉姏杈圭晫 |
| `CameraCalibration` | 绠楁硶杈圭晫 + 濂戠害 | 鍗曞浘妯″紡鍙骇鍑虹煩闃碉紝浣嗕笉搴旇璇В涓烘渶缁堢ǔ瀹氭爣瀹氾紱鏂囦欢澶规ā寮忚緭鍏ョ鍙ｈ姹備笌瀹為檯渚濊禆涓嶅畬鍏ㄤ竴鑷?| 琛ユā寮忚鏄庯紱姊崇悊鍗曞浘妯″紡杈撳嚭鐢ㄩ€旓紱鏂囦欢澶规ā寮忚緭鍏ヨ姹備笌 UI 鍚屾 |
| `Undistort` | 濂戠害 + 鏍￠獙 | 杩愯鏃堕檮鍔犺緭鍑烘湭澹版槑锛涙牎楠岃繃浜庡鏉撅紱涓嶆牎楠屽浘鍍忓昂瀵镐笌鏍囧畾灏哄涓€鑷存€?| 鍔犲己鍙傛暟/杈撳叆鏍￠獙锛涜ˉ灏哄鍏煎绛栫暐锛涜ˉ杈撳嚭澹版槑 |
| `PolarUnwrap` | 濂戠害 + 绠楁硶杈圭晫 | `Method` / `UseWarpPolar` 涓洪檮鍔犺緭鍑轰絾鏈０鏄庯紱鑷姩瀹藉害浼拌鍜岄珮搴﹁涔夊鏄撹璇В | 鏄庣‘杈撳嚭鍑犱綍璇箟锛涜ˉ杈撳嚭绔彛鎴栫粨鏋滄ā鍨?|
| `CircleMeasurement` | 濂戠害 | 澹版槑杈撳嚭 `Center` / `Circle`锛岃繍琛屾椂涓昏杈撳嚭 `CenterX/CenterY/...` | 缁熶竴鍦嗙粨鏋滄ā鍨嬪拰杈撳嚭绔彛 |
| `ColorDetection` | 濂戠害婕傜Щ | `ColorInfo` 澹版槑涓庡ぇ閲忚繍琛屾椂闄勫姞閿笉涓€鑷?| 璁捐绋冲畾鐨?`ColorInfo` 缁撴灉缁撴瀯锛屽噺灏戞暎钀介敭 |
| `Statistics` | 鏉′欢杈撳嚭濂戠害 | `USL` / `LSL`銆乣Cpk`銆乣IsCapable` 瀹為檯宸插疄鐜帮紝浣嗗彧鏈夊湪鎻愪緵瑙勬牸闄愩€佹牱鏈暟瓒冲涓旀爣鍑嗗樊澶т簬 0 鏃舵墠杈撳嚭锛屽綋鍓嶆枃妗ｅ拰涓嬫父绾︽潫鏈己璋冭繖绉嶆潯浠舵€ц緭鍑?| 鏄庣‘鏉′欢杈撳嚭瑙勫垯锛涘繀瑕佹椂澧炲姞 `HasCapabilityMetrics` 鎴栧湪鏃犺鏍奸檺鏃惰緭鍑?`null` / 榛樿鍊?|
| `Aggregator` | 濂戠害 + 瀹炵幇 | `Mode` 宸插０鏄庝絾鏈鍙栵紱褰撳墠瀹炵幇浼氱ǔ瀹氬悓鏃惰緭鍑?`MergedList/MaxValue/MinValue/Average`锛屼笌鈥滄寜妯″紡杈撳嚭鈥濈殑 UI 璁ょ煡涓嶄竴鑷?| 鏄庣‘鑱氬悎妯″紡鏄惁淇濈暀锛涜嫢淇濈暀鍒欒 `Mode` 鐪熸鐢熸晥锛屽惁鍒欎粠鍏冩暟鎹腑绉婚櫎 |
| `ContourDetection` | 濂戠害 + 瀹炵幇 | 杩愯鏃堕殣钘忚鍙?`DrawContours`銆乣MaxValue`銆乣ThresholdType`锛屼絾鍏冩暟鎹湭澹版槑锛屽鑷翠簩鍊煎寲鍜岀粯鍒惰涓轰笉鍙皟 | 鏆撮湶杞粨鎻愬彇鍓嶅鐞嗗叧閿弬鏁帮紝缁熶竴闃堝€间笌缁樺埗琛屼负濂戠害 |
| `BlobAnalysis` | 濂戠害 + 绠楁硶杈圭晫 | `MinCircularity`銆乣MinConvexity`銆乣MinInertiaRatio` 杩愯鏃剁敓鏁堜絾鏈０鏄庯紱`Color` 宸插０鏄庝絾褰撳墠瀹炵幇鏈弬涓?Blob 杩囨护 | 琛ラ綈 `SimpleBlobDetector` 鐩稿叧鍙傛暟鍏冩暟鎹紝骞堕獙璇佸弬鏁板埌 OpenCV 琛屼负鏄犲皠锛涙槑纭?`Color` 鍙傛暟鏄惁淇濈暀 |
| `ArrayIndexer` | 濂戠害 | `List` 涓?`Items` 杈撳叆瀛樺湪鍙岄敭璇箟锛沗Item` 杈撳嚭澹版槑涓庤繍琛屾椂缁撴灉閿笉涓€鑷?| **宸插畬鎴?*锛氳緭鍏ョ粺涓€涓?`List`锛堝悜鍚庡吋瀹?`Items`锛夛紱杈撳嚭缁熶竴涓?`Item`锛堝師 `Result`锛夛紱琛ュ厖 `Found/Index` 绔彛澹版槑銆?| 鉁?|
| `Comparator` | 濂戠害 | 澹版槑 `Result` / `Difference`锛岃繍琛屾椂涓嶇ǔ瀹?| 杈撳嚭妯″瀷鏀舵暃 |
| `ConditionalBranch` | 濂戠害 | 澹版槑 `True/False` 鍒嗘敮杈撳嚭锛屼絾杩愯鏃朵富瑕佽緭鍑虹粨鏋滃瓧娈?| 鏄庣‘鍒嗘敮琛屼负鏄惁杈撳嚭绔彛鍖?|
| `Comment` / `Delay` / `ResultOutput` / `TryCatch` | 濂戠害 | 澹版槑杈撳嚭涓庤繍琛屾椂杩斿洖涓嶅畬鍏ㄤ竴鑷?| 鍋氫竴杞祦绋嬫帶鍒剁被绠楀瓙鐨勭粺涓€鏀舵暃 |

## 10. P2 浼樺厛绾ф竻鍗?

P2 瀹氫箟锛氭洿澶氬睘浜庣郴缁熸€у厓鏁版嵁鏀舵暃鍜屽伐绋嬫竻娲佸害闂锛屽缓璁壒閲忔満姊颁慨澶嶃€?

### 10.1 闅愯棌鍙傛暟鎵归噺鏀舵暃

闈欐€佹壂鎻忓懡涓殑楂樹紭鍏堢骇娓呭崟锛?

- `ArrayIndexer`: `LabelFilter` ✅ 已暴露 鉁?宸叉毚闇?
- `BlobAnalysis`: `MinCircularity`銆乣MinConvexity`銆乣MinInertiaRatio`
  - 琛ュ厖璇存槑锛氳繖缁勫弬鏁板凡瀹為檯浼犲叆 `SimpleBlobDetector.Params`锛屼絾褰撳墠鍏冩暟鎹湭鏆撮湶锛屼笖搴旈澶栭獙璇佷笌 OpenCvSharp 搴曞眰琛屼负鐨勪竴鑷存€с€?
- `EdgeDetection`: `L2Gradient`
- `ClaheEnhancement`: `Channel`
- `ColorConversion`: `SourceChannels`
- `DeepLearning`: `UseGpu`銆乣GpuDeviceId`
- `OrbFeatureMatch`: `EnableSymmetryTest`銆乣MinMatchCount`
- `PyramidShapeMatch`: 闅愯棌鍙傛暟寰呬笌瀹炵幇涓€璧锋牳瀵?
- `ContourDetection`: `DrawContours`銆乣MaxValue`銆乣ThresholdType`
- `ForEach`: 闅愯棌鍙傛暟寰呮牳瀵?
- `Filtering`: 闅愯棌鍙傛暟寰呮牳瀵?
- `HistogramEqualization`: 闅愯棌鍙傛暟寰呮牳瀵?
- `HttpRequest`: 闅愯棌鍙傛暟寰呮牳瀵?
- `ImageAcquisition`: `exposureTime`銆乣gain`銆乣triggerMode`
- `ImageSave`: 闅愯棌鍙傛暟寰呮牳瀵?
- `JsonExtractor`: 闅愯棌鍙傛暟寰呮牳瀵?
- `LaplacianSharpen`: 闅愯棌鍙傛暟寰呮牳瀵?
- `MeanFilter`: 闅愯棌鍙傛暟寰呮牳瀵?
- `MqttPublish`: 闅愯棌鍙傛暟寰呮牳瀵?
- `StringFormat`: `DateFormat`銆乣Mode`銆乣Separator`

寤鸿鍔ㄤ綔锛?

1. 閫愪釜纭鏄惁搴旀寮忔毚闇层€?
2. 鑻ヤ笉搴旀毚闇诧紝鍒欑Щ闄よ繍琛屾椂璇诲彇骞跺洖褰掗粯璁よ涓恒€?
3. 琛?UI 鍏冩暟鎹祴璇曪紝闃叉闅愯棌鍙傛暟鍐嶆鍑虹幇銆?

### 10.2 宸插０鏄庝絾鏈鍙栧弬鏁版壒閲忔敹鏁?

楂樹紭鍏堢骇娓呭崟锛?

- `Aggregator`: `Mode` ✅ 已实现 鉁?宸插疄鐜?
- `CoordinateTransform`: `PixelX`銆乣PixelY`
- `HistogramAnalysis`: 閮ㄥ垎鍙傛暟寰呭鏍?
- `HistogramEqualization`: 閮ㄥ垎鍙傛暟寰呭鏍?
- `ImageSave`: 閮ㄥ垎鍙傛暟寰呭鏍?
- `ImageTiling`: 閮ㄥ垎鍙傛暟寰呭鏍?
- `NPointCalibration`: 閮ㄥ垎鍙傛暟寰呭鏍?
- `PixelStatistics`: `RoiX`銆乣RoiY`銆乣RoiW`銆乣RoiH`
- `PositionCorrection`: `CurrentAngle`
- `ResultOutput`: `Format`銆乣SaveToFile`
- `SharpnessEvaluation`: `RoiX`銆乣RoiY`銆乣RoiW`銆乣RoiH`
- `TemplateMatching`: `MaxMatches` ✅ 已实现 鉁?宸插疄鐜?
- `WidthMeasurement`: `Direction` ✅ 已实现 鉁?宸插疄鐜?

寤鸿鍔ㄤ綔锛?

1. 鏍囪涓衡€滃緟瀹炵幇鈥濇垨鈥滃簾寮冣€濄€?
2. 浼樺厛澶勭悊浼氳瀵肩敤鎴峰喅绛栫殑鍙傛暟銆?
3. 鎵归噺鍔犲洖褰掓祴璇曪紝纭繚鍙傛暟鐪熸褰卞搷杈撳嚭銆?

### 10.3 闅愯棌杈撳叆鎵归噺鏀舵暃

闈欐€佹壂鎻忓懡涓細

- `ArrayIndexer`: `Items` ✅ 已收敛（优先 `List`，兼容 `Items`） 鉁?宸叉敹鏁涳紙浼樺厛 `List`锛屽吋瀹?`Items`锛?
- `HttpRequest`: 闅愯棌杈撳叆閿緟鏍稿
- `ImageAcquisition`: 闅愯棌杈撳叆閿緟鏍稿
- `MqttPublish`: 闅愯棌杈撳叆閿緟鏍稿
- `NPointCalibration`: 闅愯棌杈撳叆閿緟鏍稿
- `PositionCorrection`: `BaseAngle`
- `TriggerModule`: `Signal`銆乣Trigger` 鉁?宸叉敹鏁涳紙姝ｅ紡澹版槑 `Signal`锛?
- `TypeConvert`: `Value` ✅ 已收敛（移除 `Value`，统一为 `Input`） 鉁?宸叉敹鏁涳紙绉婚櫎 `Value`锛岀粺涓€涓?`Input`锛?

寤鸿鍔ㄤ綔锛?

1. 鎵€鏈夐殣钘忚緭鍏ラ兘蹇呴』鏄惧紡绔彛鍖栵紝鎴栧交搴曞垹闄ゃ€?
2. 涓嬫父娴佺▼缂栬緫鍣ㄥ彧搴旀毚闇蹭竴绉嶈緭鍏ュ绾︺€?

## 11. 绠楁硶杈圭晫閲嶇偣璇存槑

浠ヤ笅闂涓嶄竴瀹氭槸 bug锛屼絾浼氱湡瀹炲奖鍝嶇敤鎴峰鈥滄晥鏋溾€濈殑鍒ゆ柇銆?

### 11.1 妯℃澘鍖归厤绫?

- `TemplateMatching`锛氶€傚悎鍗曠洰鏍囥€佸昂搴﹀浐瀹氥€佹棆杞彉鍖栧皬鐨勫満鏅紝涓嶉€傚悎澶氱洰鏍囧拰灏哄害鏃嬭浆鍙樺寲澶х殑浠诲姟銆?
- `ShapeMatching`锛氬綋鍓嶆洿鍍忔棆杞ā鏉垮尮閰嶏紝涓嶆槸瀹屾暣 shape descriptor 鍖归厤銆?
- `GradientShapeMatch`锛氶€傚悎杈圭紭缁撴瀯绋冲畾銆佸昂搴﹀彉鍖栦笉澶х殑鐩爣锛涗笉鏀寔澶氬€欓€夎緭鍑恒€?
- `AkazeFeatureMatch` / `OrbFeatureMatch`锛氶€傚悎绾圭悊鍨嬬洰鏍囷紝涓嶉€傚悎寮辩汗鐞嗐€侀噸澶嶇汗鐞嗐€佸嚑浣曚腑蹇冧弗鏍奸渶姹傚満鏅€?

### 11.2 娴嬮噺绫?

- `WidthMeasurement`锛氬綋鍓嶆槸缁熻鍨嬬偣鍒扮嚎璺濈娴嬮噺锛屼笉鏄弗鏍兼硶鍚戝弻杈规祴閲忋€?
- `GeometricFitting`锛氬綋鍓嶆槸鈥滃浘鍍?-> 浜屽€?-> 杞粨 -> 鍚堝苟鐐归泦 -> 鎷熷悎鈥濓紝涓嶉€傚悎浣滀负楂樼簿搴︾偣闆嗘嫙鍚堝櫒鐨勬浛浠ｅ搧銆?
- `CircleMeasurement` / `LineMeasurement` 绛夛細搴斿尯鍒嗏€滄樉绀虹粨鏋溾€濆拰鈥滅粨鏋勫寲娴嬮噺缁撴灉鈥濄€?

### 11.3 鏍囧畾涓庡嚑浣曞彉鎹㈢被

- `CameraCalibration`锛氬崟鍥炬ā寮忓簲瀹氫箟涓衡€滆皟璇?楠岃瘉妯″紡鈥濓紝涓嶈浣滀负楂樿川閲忕敓浜ф爣瀹氶粯璁よ矾寰勩€?
- `Undistort`锛氬綋鍓嶅彧鍋氭爣鍑?pinhole 妯″瀷鍘荤暩鍙橈紝涓嶆槸楂樼骇閲嶆槧灏勫钩鍙般€?
- `PolarUnwrap`锛氫腑蹇冪偣涓庡崐寰勫弬鏁板鏁堟灉楂樺害鏁忔劅锛屽綋鍓嶄笉鑷姩浼拌涓績銆?

### 11.4 AI 绫?

- `DeepLearning`锛氭暣浣撳伐绋嬪寲绋嬪害杈冮珮锛屼絾搴旀妸 GPU銆佹ā寮忓垏鎹€佹爣绛句紭鍏堢骇杩欎簺杩愯鏃跺叧閿涓烘樉寮忕鍙ｅ寲/鍙傛暟鍖栥€?

## 12. 寤鸿瀹炴柦椤哄簭

### Batch A锛氬绾︽琛€锛堝厛鍋氾級

鐩爣锛氬厛瑙ｅ喅鈥淯I 鐪嬭捣鏉ユ槸 A锛岃繍琛屾椂鏄?B鈥濈殑闂銆?

寤鸿瑕嗙洊锛?

- `TemplateMatching`
- `WidthMeasurement`
- `OrbFeatureMatch`
- `AkazeFeatureMatch`
- `GradientShapeMatch`
- `ImageAcquisition`
- `ContourDetection`
- `TypeConvert`
- `TriggerModule`
- `Aggregator`

### Batch B锛氳緭鍑虹粨鏋勭粺涓€

鐩爣锛氱粺涓€ `Position / X / Y / CenterX / CenterY / FitResult / ColorInfo / DetectionList` 绛夌粨鏋滄ā鍨嬨€?

寤鸿瑕嗙洊锛?

- `CircleMeasurement`
- `ColorDetection`
- `GeometricFitting`
- `DeepLearning`
- `Comment` / `Delay` / `ResultOutput` / `TryCatch`

### Batch C锛氱畻娉曡兘鍔涜ˉ寮?

鐩爣锛氫慨鐪熸浼氬奖鍝嶁€滄晥鏋溾€濈殑鑳藉姏缂哄彛銆?

寤鸿瑕嗙洊锛?

- `TemplateMatching` 鐨勫鍖归厤鏀寔
- `WidthMeasurement` 鐨勬柟鍚?娉曞悜绛栫暐涓庝簹鍍忕礌閫夐」
- `ShapeMatching` 鐨勫昂搴︽悳绱㈡垨鑳藉姏閲嶅懡鍚?
- `GeometricFitting` 鐨勫崟杞粨/鏈€澶ц疆寤撻€夋嫨
- `GradientShapeMatch` 鐨勭紦瀛樻ā鍨嬩笌澶氭ā鏉垮畨鍏ㄦ€?
- `BlobAnalysis` 鐨勫弬鏁版槧灏勬牳楠屼笌 `Color` 琛屼负鏀舵暃

### Batch D锛氫綆椋庨櫓鏈烘鏀舵暃

鐩爣锛氭竻鐞嗛殣钘忓弬鏁般€佹湭鐢ㄥ弬鏁般€侀殣钘忚緭鍏ャ€佽緭鍑哄０鏄庡櫔澹般€?

寤鸿鏂瑰紡锛?

1. 涓€娆″彧鍋氫竴绉嶉棶棰樼被鍨嬨€?
2. 姣忔壒闄勫甫鑷姩鍖栨壂鎻忓拰濂戠害娴嬭瘯銆?
3. 涓嶄笌绠楁硶鍗囩骇娣峰湪鍚屼竴涓?PR 涓€?

## 13. 寤鸿娴嬭瘯绛栫暐

姣忕被鏁存敼閮藉缓璁厤濂楁柊澧炴祴璇曪細

### 13.1 濂戠害娴嬭瘯

- 鍙傛暟闈㈡澘瀛樺湪鐨勫弬鏁帮紝杩愯鏃跺繀椤昏兘琚鍙栧苟褰卞搷缁撴灉銆?
- 澹版槑绔彛蹇呴』鑳藉湪 `OutputData` 涓ǔ瀹氭壘鍒板搴旈敭鎴栧搴旂粨鏋勩€?
- 妯″紡鍒囨崲涓嶈兘 silently 鏀瑰彉杈撳嚭瀛楁闆嗗悎锛岄櫎闈炴枃妗ｅ拰濂戠害鏄庣‘璇存槑銆?

### 13.2 鍏煎鎬ф祴璇?

- 鑰佸伐绋?JSON 鍦ㄥ弬鏁板鍒犲悗鑳藉惁骞崇ǔ鍏煎銆?
- 鐩綍鐢熸垚鍣ㄦ槸鍚︽纭弽鏄犳柊鐨勫弬鏁?杈撳嚭濂戠害銆?

### 13.3 绠楁硶鍥炲綊娴嬭瘯

- 妯℃澘鍖归厤绫伙細鏃嬭浆銆佸昂搴︺€侀伄鎸°€侀噸澶嶇汗鐞嗘牱鏈?
- 娴嬮噺绫伙細鍣０銆佽竟缂樻ā绯娿€丷OI 鍙樺寲鏍锋湰
- AI 绫伙細妯″紡鍒囨崲銆佹爣绛炬枃浠跺垏鎹€丟PU/CPU 杩愯妯″紡

## 14. 璇勫寤鸿

鍦ㄨ繘鍏ュ疄鐜板墠锛屽缓璁綘鍏堟媿鏉夸互涓嬪喅绛栵細

1. 鏄惁浼樺厛鍏煎鏃у伐绋嬶紝杩樻槸鍏佽涓€娆℃€ф敹绱ц緭鍑哄绾︺€?
2. 瀵逛簬 `Position` / `X` / `Y` / `CenterX` / `CenterY`锛屾槸鍚︾粺涓€閲囩敤缁撴瀯鍖栬緭鍑哄璞°€?
3. 瀵逛簬宸插０鏄庝絾鏈敓鏁堝弬鏁帮紝鏄紭鍏堝疄鐜帮紝杩樻槸鍏堟爣璁?deprecated銆?
4. 瀵逛簬鈥滄墽琛屾垚鍔熶絾涓氬姟 NG鈥濈殑璇箟锛屾槸鍚︾粺涓€褰㈡垚妗嗘灦绾﹀畾銆?
5. 瀵逛簬 `ShapeMatching` 杩欑被鍚嶇О鍜屽疄鐜颁笉瀹屽叏涓€鑷寸殑绠楀瓙锛屾槸鏀瑰悕杩樻槸琛ヨ兘鍔涖€?


## 15. 瀹炴柦璁板綍

### 2026-03-15 瀹屾垚 Batch A 濂戠害姝㈣

鏈鏇存柊瀹屾垚浜嗚鍒掓枃妗ｄ腑 **Batch A锛氬绾︽琛€** 鐨勫叏閮ㄧ畻瀛愪慨澶嶏細

| 绠楀瓙 | 淇鍐呭 | 鐘舵€?|
|------|---------|------|
| `TemplateMatching` | 缁熶竴 `Method` 鏋氫妇锛?绉嶆柟娉曞叏鏀寔锛夛紱瀹炵幇 `MaxMatches` 澶氱洰鏍囧尮閰嶏紱`Position` 鏍囧噯鍖栦负涓績鐐?| 鉁?|
| `WidthMeasurement` | 瀹炵幇 `Direction`/`CustomAngle`锛涗簹鍍忕礌妫€娴嬶紱`ImageWidth/ImageHeight` 涓?`Width` 鍒嗙 | 鉁?|
| `GradientShapeMatch` | 淇缂撳瓨閿紙鍚ā鏉垮搱甯岋級锛涙樉寮忚緭鍑?`Position`锛汱RU缂撳瓨锛堟渶澶?鏉★級 | 鉁?|
| `OrbFeatureMatch` | 鏆撮湶 `EnableSymmetryTest`/`MinMatchCount`锛涚粺涓€ `Position` 杈撳嚭锛涘榻愬け璐ヨ涔?| 鉁?|
| `AkazeFeatureMatch` | 琛ュ叏 `Position` 杈撳嚭锛沗Score` 瀹氫箟涓哄唴鐐规瘮渚嬶紱缁熶竴澶辫触璇箟 | 鉁?|
| `DeepLearning` | 鏆撮湶 `UseGpu`/`GpuDeviceId`锛涙樉寮忓０鏄?`DetectionList` 绔彛 | 鉁?|
| `ImageAcquisition` | 鍏冩暟鎹０鏄?`ExposureTime`/`Gain`/`TriggerMode`锛涗繚鐣欏弬鏁板悕鍏煎鏄犲皠 | 鉁?|
| `TypeConvert` | 缁熶竴杈撳叆閿负 `Input`锛涜ˉ鍏?`AsString/AsFloat/AsInteger/AsBoolean/OriginalType` 杈撳嚭绔彛 | 鉁?|
| `TriggerModule` | 姝ｅ紡澹版槑 `Signal` 杈撳叆绔彛锛涚Щ闄?`Trigger` 闅愯棌閫昏緫 | 鉁?|
| `Aggregator` | 瀹炵幇 `Mode` 鍙傛暟锛涚ǔ瀹氳緭鍑?`MergedList/MaxValue/MinValue/Average` | 鉁?|
| `ContourDetection` | 鏆撮湶 `DrawContours`/`MaxValue`/`ThresholdType` 鍙傛暟 | 鉁?|
| `BlobAnalysis` | 鏆撮湶 `MinCircularity`/`MinConvexity`/`MinInertiaRatio` 鍙傛暟 | 鉁?|
| `ArrayIndexer` | 杈撳叆缁熶竴涓?`List`锛堝悜鍚庡吋瀹?`Items`锛夛紱杈撳嚭缁熶竴涓?`Item`锛涜ˉ鍏?`Found/Index` 绔彛 | 鉁?|

**閰嶅娴嬭瘯**锛?
- `Sprint2_ArrayIndexerTests.cs`锛?0涓祴璇曠敤渚嬶紝瑕嗙洊绱㈠紩/鏈€澶х疆淇″害/鏈€澶ч潰绉?鏈€灏忛潰绉?鏍囩杩囨护/绌哄垪琛?瓒婄晫/鍚戝悗鍏煎/濂戠害涓€鑷存€?
- `OperatorContractReconciliationTests.cs`锛?5涓绾﹀洖褰掓祴璇曪紝瑕嗙洊鍏冩暟鎹０鏄庝笌杩愯鏃惰涓轰竴鑷存€ч獙璇?

## 16. 缁撹
