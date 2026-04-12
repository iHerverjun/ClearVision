// PromptBuilder.cs
// 闂佸湱绮崝妤呭Φ濮樿鲸瀚氱€广儱妫涢埀顒夊灠椤曟瑩宕崟顒傚綔
// 闂佸搫绉烽～澶婄暤娴ｅ湱鈻旀慨姗嗗墮椤倕鈽夐幘绛规缂佹鎳樺顒勫炊閵娿儺浼濋柣鐔剁閹冲繗鍟梺鎼炲妼椤戝牓鎯冮悢鍏煎仺闁靛绠戦。鏌ユ⒒閸ワ絽浜鹃梺鍦帛閸旀濡靛杈ㄥ珰鐎广儱鎳庨弫鍫曟倵?
// 婵炶揪绲剧划蹇涘焵椤掆偓閹锋垹妲愭导瀛樻儚闁告稒婢橀～姘舵煕?
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Services;

namespace Acme.Product.Infrastructure.AI;

/// <summary>
/// 闂佸搫顑呯€氼剛绱撻幘璇茬煑闁硅揪鑵归崑鎾存媴閾忕鍩?AI 闂?System Prompt
/// </summary>
public class PromptBuilder
{
    private readonly IOperatorFactory _operatorFactory;
    private static readonly JsonSerializerOptions _catalogJsonOptions = new()
    {
        WriteIndented = true
    };

    public PromptBuilder(IOperatorFactory operatorFactory)
    {
        _operatorFactory = operatorFactory;
    }

    /// <summary>
    /// 闂佸搫顑呯€氼剛绱撻幘鍨涘亾閻熺増婀伴柡鍡秮閹?System Prompt
    /// </summary>
    public string BuildSystemPrompt(string? userDescription = null)
    {
        var sb = new StringBuilder();

        AppendSection(sb, "Section 1 - Role And Hard Rules", GetRoleDefinition());
        AppendSection(sb, "Section 2 - Domain Workflow Patterns", GetDomainKnowledge());
        AppendSection(sb, "Section 3 - Template First Strategy", GetTemplateFirstStrategy());
        AppendSection(sb, "Section 4 - Phase 1 Operator Extensions", GetPhase1OperatorExtensions());
        AppendSection(sb, "Section 5 - Phase 2 Operator Extensions", GetPhase2OperatorExtensions());
        AppendSection(sb, "Section 6 - Phase 3 Operator Extensions", GetPhase3OperatorExtensions());
        AppendSection(sb, "Section 7 - Operator Catalog", GetOperatorCatalog(userDescription));
        AppendSection(sb, "Section 8 - Connection Rules", GetConnectionRules());
        AppendSection(sb, "Section 9 - Parameter Inference Guide", GetParameterInferenceGuide());
        AppendSection(sb, "Section 10 - Output Format", GetOutputFormatSpec());
        AppendSection(sb, "Section 11 - Few Shot Examples", GetFewShotExamples());

        return sb.ToString();
    }

    private static void AppendSection(StringBuilder sb, string title, string content)
    {
        if (sb.Length > 0)
            sb.AppendLine();

        sb.AppendLine($"## {title}");
        sb.AppendLine(content.Trim());
    }

    private string GetDomainKnowledge() => """
        # 婵☆偅婢樺Λ妤呮偂濞嗘挻鍎楅柕澶堝妿濡叉洖鈽夐幘铏攭妞ゆ梹娲滈幏瀣焺閸忊€茬椤?

        ## ClearVision 濡ょ姷鍋涢崯鑳亹閺夋埈娼￠柛灞剧箓閻?
        ClearVision 闂佸搫瀚烽崹顖滅博鐎涙鈻旀い蹇撶墛濡椼劑鏌涘顓炵仴濞村吋鍔栫粙澶婎潩椤掆偓閻撴垿姊洪銈呅ユ繛鍫熷灴瀹曠兘濡歌濞堢娀鎮峰▎蹇旑棥妞ゎ偄楠歌灋闁逞屽墮闇夐悗锝庝邯閹割剟鏌涘▎鎺戠劷闁?
        婵炴垶鎸诲Σ鎺椼€呴敃鍌氬珘鐎广儱鎳庨～銈夋偠濞戞ê顨欑紒妤冨枛閺?C 闂佹眹鍨藉褔鎮哄▎鎾存櫖闁割偓绲借ⅸ闂?闁哄鏅濋崑鐐垫暜閹绢喖闂?FPC闂佹寧绋戦ˇ顓㈠焵椤戣法鍔嶉柤娲诲灡濞碱亪鏁冮崒娑氥偐闂備緡鍠撻崝瀣枎閵忋倖鏅柛顐秮閸欙繝鏌?婵帗妲掗崕濠氭偪?闁诲酣娼уΛ妤呮儍濠靛洨顩烽悹浣告贡缁€鍡涙煏?
        婵＄偛顑囬崰搴ㄥ箹瑜斿畷鐘诲川闁附顥婇梺鎸庣☉閻楀﹪鎮ラ敐鍥╅┏?闂佸搫鍟ㄩ崕鎻掞耿?闂佺儵鈧啿顣虫繛鎻掞躬閺佸秵寰勯崼姘壕濞达絿顭堢壕褰掓倵閻㈠憡锛熺紓宥咁儔閺佸秹宕奸悢鍝ュ娇闂?閻庢鍠楀ú婊堝吹鎼粹槅娴栭柛鈩冾焽娴犳悂鏌ㄥ☉姗嗘Ф闁逞屽厸鐠у━B/SMT闂佹寧绋戦悧蹇涘礈閵娾晜鍊?闂佺绻愰崯鈺佲枎閵忋倖鏅璺虹墐閸?
        濡ょ姷鍋涢崯鑳亹閹绢喗鐒绘慨妯虹－缁犳牠鏌ら崫鍕偓濠氬磻閿濆拋鍤曢煫鍥ㄦ尰缁傚牏鎲搁懜顒€鐏╅悗浣冨皺缁辨捇寮介鐔哥厾闂佹寧绋戦悧濠勨偓姘儔楠炲繘鎮惧畝鈧弳顒勬倵?+ 闁哄鏅濋崑鐔煎吹鎼淬劍鏅璺哄瘨閸炪劎鈧鎮堕崕鎶藉煝閼测晜鏆滈柛顐ゅ枑閿熴儵姊婚崶锝呬壕缂傚倸鍊归悧婊堟偉濠婂牆纭€闁告劖褰冪拋鏌ユ煛鐎ｎ亜顏╃紓鍌涙尭铻為柍褜鍓欓湁閻庯綆浜欑槐锝吤归敐鍡欑煁缂佷緤绠撴俊?

        ## 闁汇埄鍨遍悺鏇綖閸℃鍟呴柕澶堝€楃粙濠冪箾缂堢姵绁版い鏃€娲滈幏瀣焺閸忊€茬椤?

        ### 濠碘槅鍨埀顒€纾涵鈧?1闂佹寧绋掗惌顔炬閸撲胶纾奸柣鏂挎惈缁讳線姊婚崟顖涙暠妞ゃ儱鎳庨湁?
        闂備緡鍋勯崐濠氬极閵堝鎹堕柣鎴炆戦悵顖炴煥濞戞瑧鐓Δ鐘叉喘瀹曨偅鎷呮慨鎰ㄥ亾閸愵喗顥堥柕蹇曞Т閻忓﹪鏌?濠殿喚娅㈢槐鏇㈠磻?缂傚倸鍊婚崕銈呯暦?婵犮垼鍩栭惌顔剧礊閹达附鍋嬮柍鍝勫亞濮婄偓绻?
        闂佺绻掗…鍫ユ倵瀹勬噴瑙勬媴鐞涒剝鐓犻梺鎸庣⊕椤戞ageAcquisition 闂?Filtering(闂傚倸瀚粔鏉戔枍? 闂?Thresholding(闂佸憡甯掑Λ妤佺珶? 闂?BlobAnalysis(缂傚倸鍊瑰钘夆枍閿熺姴绠甸柟閭﹀墮缁? 闂?ResultJudgment(OK/NG) 闂?ResultOutput
        闂佺绻戞繛濠囧极椤撱垺鍊烽柣褍鎽滅粣妗瀕ob闂佹眹鍔岄崵娓媙Area/MaxArea闂傚倸娲犻崑鎾绘偡閺囨氨鍔嶉柣妤佹尦楠炴垿顢欓幆褏绠掗梻鍌氬閸㈡煡濡甸崶鈺€鐒婇煫鍥ㄥ嚬閸熷酣鎮楃憴鍕暡缂佽精椴告穱濠囧磼濞戞矮鍑介梺鐓庡暱閺堫剙鈻嶅▎鎰枖鐎广儱鎳忕紞鍡涙煕閺嶃倕浜炬繛鏉戝悑閿氶柛妯荤洴閹€熸％daptiveThreshold

        ### 濠碘槅鍨埀顒€纾涵鈧?2闂佹寧绋掗濠?濠电儑绲藉畷顒傗偓鐟扮－閳ь剚鍐荤紓姘卞姬閸曨倠娑㈠焵椤掆偓闇?
        闂備緡鍋勯崐濠氬极閵堝鎹堕柣鎴炆戦悵顖炴煥濞戞瑨澹樻い锔界叀瀵爼宕掑鍕綎闂佽崵鍋涘Λ婊堝Υ閸愵喗顥堟い顐厴閸嬫挻鎷呮搴ｅ€炵紓鍌欒兌閸犳捇鎮剧拠娴嬫灃闁哄洢鍨洪娑樏归悩顔煎姎闁搞劌閰ｅ畷婊堟嚑椤掍胶鏆犵紓鍌氬€瑰钘夆枍?
        闂佺绻掗…鍫ユ倵瀹勬噴瑙勬媴鐞涒剝鐓犻梺鎸庣⊕椤戞ageAcquisition 闂?ImageResize(闂備緡鍋勯崐鍧楀储閵堝憘鐔煎灳瀹曞洠鍋撻柨瀣秶闁规儳鍟垮鎶芥倶韫囨挻鎯堟い? 闂?DeepLearning 闂?ConditionalBranch(缂傚倸鍊瑰钘夆枍閿熺姴鏋?0?) 闂?ModbusCommunication(NG婵烇絽娲犻埀顒€鍟挎繛? / ResultOutput
        闂佺绻戞繛濠囧极椤撱垺鍊烽柣褍鎽滅粣妗猠epLearning闂佸憡鎸哥粔宕囩博閹绢喗鍤嬫い鎺戝娴犳﹢鎮烽弴姘癁mageResize闂佸憡甯炴晶妤勫暞闂佹悶鍔岄鍫ャ€呴敃鈧晥闁稿本绮嶉悾閬嶆倶韫囨挻鎯堟い?婵?40闁?40)闂佹寧绋掔粙鎰箔婢跺本鍟哄ù锝呮啞闊徏eepLearning闂佸憡鑹鹃柊锝咁焽娴兼潙绀冪€广儱妫楁径宄奾resholding

        ### 濠碘槅鍨埀顒€纾涵鈧?3闂佹寧绋掗懝楣冨及閸屾壕鍋撻棃娑欏暈缂佷焦鎸抽弻?
        闂備緡鍋勯崐濠氬极閵堝鎹堕柣鎴炆戦悵顖炴煥濞戞瑥鍝烘繛鏉戝€圭粋鎺旀崉閾忚鎲伴悗?闂傚倸鍊婚ˉ鎰玻?闁荤喐鐟︾敮鎺斺偓?闁哄鍎愰崰鏍垝閵娧傜剨闁告繂瀚烽崵鐔兼煟閵娿儱顏紒銊ㄦ硶閳ь剟娼уΛ娆戠矈閹绢喗鐓?
        闂佺绻掗…鍫ユ倵瀹勬噴瑙勬媴鐞涒剝鐓犻梺鎸庣⊕椤戞ageAcquisition 闂?Filtering 闂?EdgeDetection 闂?CircleMeasurement/LineMeasurement 闂?Measurement(闁荤姷鍎ょ换鍕€? 闂?CoordinateTransform(闂佺绉寸换鎺旂矆瀹€鍕劶闁圭儤鎼╅崵鎴犵磼? 闂?ResultOutput
        闂佺绻戞繛濠囧极椤撱垺鍊烽柣褍鎽滅粣妤佺箾閺夋埈鍎旈柛锝堟閻氶箖鎳￠妶鍥ㄦ瘓闁诲孩绋掗崝鎺旂博閹绢喗鍤嬫い鎺戝娴犳﹢鎮烽弴姘鳖槮闁告ɑ鐩畷鎴濐潩瀹曞洨褰剧紓鍌氬€硅摫妞ゃ儱鎳庨湁閻庯綆鍣弳鏇烆熆鐠哄搫顏柟顔硷躬閺佸秴鈽夊▎鎰帛缂傚倷绀侀悧鎰般€呴敃鍌涘仺闁炽儲鎮恛rdinateTransform闁诲繐绻愬Λ妤呭磿濮樿鲸顫曢柣妯荤ゴ閸嬫捇鎳濋悧鍫偖婵炴垶鎹佸▍锝嗘櫠閸ф鍋犻柛鈩冾殕濡测偓闁?mm)

        ### 濠碘槅鍨埀顒€纾涵鈧?4闂佹寧绋掔喊宥咁焽椤栫偞鍎?OCR闁荤姴娲ゅΛ妤呭春?
        闂備緡鍋勯崐濠氬极閵堝鎹堕柣鎴炆戦悵顖炴煥濞戞瑧鐓Δ鐘叉喘瀹曨偅鎷呴懞銉ヮ伓濠电姍灞界仜闁逞屽厸缁€浣衡偓鍨焽閹叉宕ㄩ璺ㄧ畾闂佽鍙庨崹鐢割敋娴兼潙鐭?
        闂佺绻掗…鍫ユ倵瀹勬噴瑙勬媴鐞涒剝鐓犻梺鎸庣⊕椤戞ageAcquisition 闂?CodeRecognition/OcrRecognition 闂?ConditionalBranch(闂佸憡鍔曢幊搴敊閹版澘绀岀憸鐗堝笒鐢?) 闂?DatabaseWrite/ModbusCommunication 闂?ResultOutput

        ### 濠碘槅鍨埀顒€纾涵鈧?5闂佹寧绋掗懝楣冨垂鎼淬劌绠?闂佸憡甯掑Λ娑欘殽?
        闂備緡鍋勯崐濠氬极閵堝鎹堕柣鎴炆戦悵顖炴煥濞戞瑧顣查悗鍨矊铻為柍褜鍓欓湁閻庯綆鍓涘▔銏ゆ煛鐎ｎ偆鐭婄憸鏉垮€块弻鍛媴妞嬪海鎲归梺鍛婅壘婵傛梹绌辨繝鍥х煑?
        闂佺绻掗…鍫ユ倵瀹勬噴瑙勬媴鐞涒剝鐓犻梺鎸庣⊕椤戞ageAcquisition 闂?(濠碘槅鍋€閸嬫挻绻涢弶鎴剳闁伙綀宕甸埀? 闂?ConditionalBranch 闂?ModbusCommunication(OK婵烇絽娲犻埀顒€鍟挎繛? / ModbusCommunication(NG婵烇絽娲犻埀顒€鍟挎繛? 闂?ResultOutput
        闂佺绻戞繛濠囧极椤撱垺鍊烽柣褍鎽滅粣妗無nditionalBranch闂佹眹鍔岄崵鐬╱e/False婵炴垶鎸堕崐娑㈡儗妤ｅ啫绀嗛柛鈩冾殔閻掑ジ寮堕埡鍐ㄤ粧缂佹顦靛畷銉ョ暆閳ь剙鈻撻幋锔界劵婵﹩鍋傜换鍡欑磼閻樻剚娈旈柣?

        ### 濠碘槅鍨埀顒€纾涵鈧?6闂佹寧绋掗懝楣兯囬崹顕呭晠闁靛鍊楃粔瀵糕偓鐢稿亰娴滅偤鐛崱妞㈡盯鍩€椤掆偓闇?
        闂備緡鍋勯崐濠氬极閵堝鎹堕柣鎴炆戦悵顖炴煥濞戞瑨澹橀柟顔芥尰缁嬪鍩€椤掍胶顩茬憸宥夊箹瑜庡鍕潩椤愶箑娈濇繛杈剧到缁夊爼鎮块崱妞㈡盯鍩€椤掆偓闇夐悗锝冨妷閸嬫挻鎷呴悷閭︽闂備焦褰冪换妤冩崲濞戞氨纾兼い鎾跺櫏濮婄偓绻?
        闂佺绻掗…鍫ユ倵瀹勬噴瑙勬媴鐞涒剝鐓犻梺鎸庣⊕椤戝窢cleCounter 闂?ImageAcquisition 闂?(濠碘槅鍋€閸嬫挻绻涢弶鎴剳闁伙綀宕甸埀? 闂?DatabaseWrite 闂?ResultOutput

        ## 闂佹椿娼块崝宥夊春濞戙垹鐭楅柨婵嗙墱閸?闂?缂備胶濮甸〃鍛存偤濞嗘挸鍙婇柣妯垮皺濞堟悂姊洪銈呮瀾闁?

        | 闂佹椿娼块崝宥夊春濞戞碍鍋橀柕濞垮妼閻?| 闁圭厧鐡ㄥΛ鍐焵椤掆偓椤﹂亶鎮鹃懡銈傚亾?|
        |---------|---------|
        | "闂佺懓鍢茬粔鍫曞矗?闂佸憡鐟﹂悧鏇灻?闂佺鍩栭幐绋棵? | ImageAcquisition |
        | "闂佸憡顭堥褍鈻?闂傚倸瀚粔鏉戔枍?濡ょ姷鍋犲▍鏇犲垝?濠碘槅鍨界槐鏇犳兜? | Filtering 闂?MedianBlur闂佹寧绋戦悧濠囁囬悜鑺ュ剮闁归偊鍓氶悵銊ヮ熆鐟欏嫬顥嬮柡渚囨＞edianBlur闂?|
        | "婵炲瓨绮岄懟顖炲焵椤掑倸甯堕悗?婵帗绋掗崹鐟扳枍?闂佸憡甯掑Λ妤佺珶婵犲洤绀堢€广儱妫欓悵? | Thresholding闂佹寧绋戦悧鍡楊焽鎼淬劌绀岄柍褜鍓熷畷妤佸緞鐏炶棄瀛ｉ梺鎸庣☉椤︿即宕?AdaptiveThreshold闂佹寧绋戦悧鍛箔婢舵劕閿ら柛銉戝啰妲梺绋跨箰椤﹂亶宕ｆ惔銊︽櫖?|
        | "闂佸憡顭囩划顖炴儊濞差亜绀?婵犻潧顦介崑鍕偤?闂佽壈缈伴崝蹇涘磿?闂佺厧鐏氶崝妯衡枔? | Morphology |
        | "闂佺懓鐏氬畷妯肩博?闂佸湱绮崝姗€鎮块崱妤婂殘?Canny" | EdgeDetection |
        | "闂佺懓鐏氶崕鍐层€?闂侀潻绠戝Λ妤呮偤?闁诲孩绋掗弻銊ф? | CircleMeasurement |
        | "闂佸綊娼ф晶浠嬪吹?闂佺儵鏅涢顓㈠吹?闁荤喐鐟︾敮鎺斺偓? | LineMeasurement |
        | "濠电偞娼欓鍫㈢玻?闂備焦褰冪换鎰板及閸屾壕鍋?闂傚倵鍋撻柛顭戝枛椤? | Measurement |
        | "闁哄鍎愰崜姘额敋閻樼數灏?闂佺粯銇涢弲婵嬪箖婵犲洤閿ら柟閭﹀幘閸? | CoordinateTransform |
        | "AI濠碘槅鍋€閸嬫挻绻?濠电儑绲藉畷顒傗偓鐟扮－閳ь剚鍐荤紓姘卞姬?YOLO" | DeepLearning |
        | "闂佽顔栭崑鍡涙偉?婵炲瓨绮岄惉鐓幥庨鈧幆?闂佸搫顧€缁辨洟鎮? | CodeRecognition |
        | "闂佸憡甯囬崐鏍蓟閸㈡窚/NG/闂佸憡鑹鹃悧濠囨偋? | ResultJudgment 闂?ConditionalBranch |
        | "闂佸憡鐟﹂崢绯扖/闂備緡鍋呴惌顔界┍?闂佸憡鐟﹂崹铏┍婵犲洤鐭? | 闂佹椿浜滈鍛村箹瑜旈幃褍鐣濈€ｎ剛鐛ラ柣鐔割殔濞尖€澄涢鍌楀亾濞戞瑥濮€闁哥喎鈹榠emensS7Communication闂佹寧绋戞總鏃傜箔娓氣偓閹筹綁骞嶆担鍛婃珨MitsubishiMcCommunication闂佹寧绋戦張顒勵敄閸屾稒鍙忛柛鈩冪〒閻戯箓鏌嶉锝呪拺mronFinsCommunication闂佹寧绋戦惌鍌炲焵椤掍焦鐨戦柡浣靛€濋崺鍡涘箥椤ｆ槴busCommunication |
        | "闁诲孩绋掕摫闁哄棛鍠栭獮鎴︻敊閼测晜鐨?闁荤姳鐒﹀妯肩礊? | DatabaseWrite |
        | "缂傚倸鍊甸弲婊堝棘?闂佽　鍋撻柟顖嗕椒娴?缂傚倸鍊甸弲娑㈡儍? | ImageResize |
        | "闁荤喍妞掔粈浣圭珶閳?ROI" | ImageCrop 闂?RoiManager |
        | "婵☆偆澧楃划蹇旂珶?闂佺顑呯换妤佺珶?闂佽壈顫夎ぐ鍐ㄎ? | ColorDetection |
        | "闂佸搫绉村ú銈夋偩?闂佷紮绲块…鍫ｃ亹婢舵劕鍐€闁斥晛鍠氶崝鈧? | CameraCalibration 闂?Undistort |
        | "濠碘槅鍨崜婵嗩熆濡皷鍋撶憴鍕叝缂?闂佺懓鐏氶崕鍐裁? | TemplateMatching 闂?ShapeMatching闂佹寧绋戦悧鎾炽€掗崜浣瑰暫濞达綀顫夐ˉ瀣级?闁诲繐绻愰幖顐も偓纭呮珪閵囨梹鎷呯拠鈩冾棞闂佸搫鍟﹢鍦椤撱垹绀傞柛顐犲灪閺?ShapeMatching闂佹寧绋掔粙鎰偓鍨笚閹棃鏁傞挊澶屾闂佸憡鏌ｉ崝宀勫Φ閸ヮ剙绫嶉柡鍫㈡暩閸犳﹢鏌?TemplateMatching 闂?Edge/Gradient 闂佺硶鏅濋崳锔炬?|
        | "闁诲簼绲婚～澶愭儊椤旇姤鍎熼柨鏇楀亾妞ゃ倕鍟?闂佺儵鏅涢悺銊╁蓟閻旂厧鐐? | HistogramEqualization |

        ## 闂佸憡鐟ョ粔鐟邦焽閺夎鐔煎灳瀹曞洨顢呴梺鎸庣☉閻楀棛鎹㈤埀顒€顪冮妶鍜佺吋濞村皷鏅犲畷妤€顓奸崶銊ф殸闂備焦瀵ч悷銊╊敋閵堝洦濯奸柟瑙勫姦閸氣偓闂?

        1. **婵炴垶鎸哥粔鐑姐€呴敃鍌氭嵍?DeepLearning 闂佸憡鑹剧€涒晝鏁?Thresholding**
           AI 濠碘槅鍨埀顒€纾埀顒€鍢查蹇涙嚑閸撲胶鐐曢梺鍛婂灩閸庛倝藟閸涱劶鍦偓锝庡墰濞夈垽鏌＄€ｎ偆鐭婇柟閿嬪缁辨棃顢欑拋宕囩畾闁硅壈鎻拋锝囨濠靛绀冪€广儱鎳嶇划闈浢瑰鍐闁逞屽墰閸樠呪偓鍨絻閳讳粙鍩勯崘鈺冪暢闂佽婢樼换瀣不?
        2. **濠电偞娼欓澶愬闯閾忓湱涓嶆俊銈傚亾闁烩剝鐟︾粙澶婎吋閸偅鐭楅梺鐑╂櫅閻°劎鏁€涙ɑ浜ら柣鎰级缁傚牓鏌涘鍛缂?*
           CircleMeasurement/LineMeasurement 闂傚倸娲犻崑鎾绘偡閺囨氨顦﹂柛妯荤〒缁辨帟绠涘杈╃暫 EdgeDetection 婵☆偅婢樼€氼剟藝閳哄懏鍋犻柛鈩兠·鍛存煠鐎圭姵顥夌紒鍫曚憾瀹曟岸鎮ч崼銏╀紩闂佸搫鎳忔刊鐣岀博閻旇櫣纾?
        3. **闂傚倸娲犻崑鎾绘偡閺囨俺鍏屽褍娼￠幃鍫曞幢濡や焦顫呴柣搴ㄦ涧閹诧繝宕归妸鈺佸珘闁告繂瀚悵銈吤瑰鍫㈢窗閽樼喖鏌涚€ｎ厽纭舵繛鍫熷灴瀹曠兘鎮滃Ο鑽ゅ讲婵炴垶鎸哥粔鐑姐€呴敂鐐磯閻庡湱濮风粻鏍煛瀹ュ懏鎼愰柣锝夌畺閺?*
           婵犵鈧啿鈧綊鎮樻径鎰仺闁靛绠戦悡鏇㈡煙缂佹ê濮囬柛?濠殿噯缍嗛崑鍡涙嚄?"閻庣敻鍋婇崰姘舵嚄?闂侀潧妫旈悞锕€鈹冮埀顒佹叏婵犲啫顎滈柛锝忓閹即濡烽妷銈囩礆闂佺懓鐡ㄩ悧婊勬櫠閸ф鍋犻柛鈩冾殕缂嶅繘鏌″鍛カ缂佽鲸绻堝鐢稿焵椤掑倻纾奸柛顐ｇ箘缁犳垵顪冮妶鍫濆惞缂侇喒鍓濆?PixelToWorldTransform / CoordinateTransform / 闂佸搫绉村ú銈夋偩閸撗呯＜闁规儳顕禍?
        4. **闂備緡鍋呴惌顔界┍婵犲嫮涓嶆俊銈傚亾闁烩剝鐟︾粙澶婎吋閸偅鐭楁繛鎴炴崄鐏忣亝绂掗崼鐔稿閻犳亽鍔嶉弳?*
           婵犵鈧啿鈧綊鎮樻径鎰梿闁逞屽墰閹茬増鎷呴崨濠傗偓閬嶆煛閸愵厽纭剧憸?OK/NG 婵烇絽娲犻埀顒€鍟挎繛鍥煥濞戞瀚扮紒銊ㄥ皺閹风娀濡烽妶鍥╃厾 ConditionalBranch 闂?True/False 闂佸憡鑹剧€氼垳鎹㈠☉娆戔枖闁逞屽墯缁嬪顢旈崼姘壕婵﹩鍋傜换鍡欑磼閻樻剚娈旈柣鈩冪懇閺佸秹宕煎┑濠傜彲闂佽壈椴稿Λ鎴犳濮樿埖鏅悘鐐跺亹閻熸繈鎮烽弴姘樂閻熸洖妫濋幊?
        5. **缂傚倸鍊瑰钘夆枍閳ユ緞娑㈠焵椤掆偓闇夐悗锝庡亞閵堬妇绱掔€ｎ亶鍎庣紒妤€顦遍幉鐗堢瑹閳ь剚绂掗幇顒夌叾?ResultOutput**
           濠殿噯绲界换瀣煂濠婂應鍋撻悷鐗堟拱闁哄棴绲介蹇涘Ψ閵堝洨鈻曞┑鐐电節缁舵岸宕楀Ο缁樺劅闁哄倸褰炵花?ResultOutput 缂傚倷鐒﹂幐鎼佹偄椤掑嫭鏅€光偓閳ь剟寮妶鍡欘洸閹兼番鍨诲﹢浠嬫煙椤掑喚鍤欓柟閿嬪娴狅箓寮撮悩顔荤驳缂傚倷鐒﹂幐濠氭倶?
        6. **婵炴垶鎸哥粔鐑姐€呴敂鐐磯閻庡湱濮风粻鏍涢弶鍨仼妞わ腹鏅犻幃?*
           閻庤鎮堕崕鎵箔閻旂厧鐐婇柟顖嗗啫澹栭梻渚囧亝閼归箖鎮ラ崼鏇炲珘濠㈣泛锕ラ悵銊ヮ熆閻熸澘绨荤紒杈ㄧ箞閹嫮鈧稒锚婢跺秹鏌涚€ｎ偂浜㈢紒鏃傚枔缁辨挸螣濞茬粯鈷曞┑鐐存綑椤戝棝宕硅棻lob闂佸憡甯掑Λ娆撴倵閼恒儱顕辨慨姗嗗亰閻涙捇鏌ｉ姀銏犳瀻闁靛洤娲弻宀冪疀濮樼厧娈╁┑鈽嗗亐閸嬫捇鏌ㄥ☉妯垮缂併劍鐓″畷?Filtering
        """;

    private string GetTemplateFirstStrategy() => """
        # 濠碘槅鍨崜婵嗩熆濡顕辨俊顖氭惈鐢儳绱掑☉娆戝⒈闁哄棙鍔欓弫宥夊醇閵壯冭拫婵☆偆澧楅崹闈涒攦閳ь剟鏌￠崪浣哥伈缂?

        閻熸粎澧楅幐楣冨极閵堝绠ｉ梺鍨儏娴煎酣寮堕埡鍌涚凡闁规枼鍓濈粙澶愵敇濠靛懐闉嶆繛鎴炴尭椤戝懘宕ｈ閺屻劑顢欑捄銊π撻梺鍝勫暢椤旀劗妲愬┑鍥ь嚤婵☆垰鎼敮銉╂偣瑜庢竟瀣焵椤掍胶鐭嬮懚鈺呮煛婢跺浠掔紒槌栧弮瀹曟宕煎锝呬壕婵犻潧艌閸嬫挾浠﹂懖鈺冩喒闂佸搫瀚烽崹顏堝焵椤掍胶鐭婇柛娆忕箻閺屽矁绠涘顒侇唶闂佹眹鍨兼禍顒傛椤忓牆绠抽柟鍝勬閸嬫挸鈹戦幘鍓佺崶
        - 缂備焦鍎抽悘婵堣姳?/ 缂備焦妫忛崹浼存偤?/ 闂佽浜介崕鎶藉吹鎼搭潿浜滈柛婵嗗绾?/ 闂佸湱鍎ょ敮锟犲箯閳╁啨浜滈柛婵嗗绾?
        - wire sequence / terminal order / connector order

        闁诲海鏁搁幊鎾诲箠閳╁啰鈻旀い鎾跺仦缁ㄦ岸鏌￠崪浣哥仯婵炲牊鍨归幉鐗堟媴閸︻厽鍕鹃梺?
        1. 婵炴潙鍚嬮敋闁告ɑ绋掑鍕吋閸ャ劍娈㈡繛鎴炴尭妤犵鈹冮埀顒勬煛閸滀礁鐏℃繛鎾崇埣瀹曠姾銇愰幒鎴濊祴闂佹眹鍔岀€氼叀鍟梺鍝勵槼濞夋洜鍒掗妸鈺佸嚑闁告洖澧庣粈澶愭煕閹邦剛啸閽樼喖鏌涜箛鎾村櫣缂佺儵鍋撻柣鐔告磻閼冲爼鎮鹃懡銈傚亾濞戞瑥濮冪紒妤€鐭傚畷锝夊磼濞戞瑦顔嶉梺?
        2. 婵炴垶鎸哥粔鐑姐€呴敂鐣岊浄閹兼番鍔嶇粊鈺冣偓娈垮枓閸嬫挸鈹戦纰卞剱闁诲寒鍨堕弻鍛存偐閾忣偓楠忛梺绋跨箰绾绢參鎮鹃懡銈傚亾濞戞瑥濮嶉柟钘夈偢閺佸秶浠﹂懖鈺冾啍闂佸綊鏅查悞锕佸暞闂佸搫顦妵妯尖偓闈涚焸閻涱噣鎳犻銏℃⒒閳ь剝顫夐崫搴ㄥ焵?
        3. 闁哄鐗婇幐鎼佸吹椤撶喓鈻旀い鎾跺仧鐎瑰鏌涢弽褎鍣归柟顖氱墦楠炴帡濡搁妸銉ь槰濠碘槅鍨崜婵嗩熆濮椻偓婵″瓨鎷呴崫銉ь€€缂佺虎鍙庨崰娑㈩敇婵犳艾鐭楅柛灞剧⊕濞堝爼鏌曢崱鏇″厡鐎规瓕椴稿鍕綇椤愩們鍋掑┑鐘欏嫬濮冨ǎ鍥э躬楠炰線顢涢妶鍥╊槷婵炴挻鑹鹃妵妯艰姳椤掆偓椤斿繘濡疯閺屻倝鎮介姘卞闁圭儵鏅犻弻鍛存偄缂佹ê鍔欓梺闈╃畳閸╁洭鍩€?
        """;

    private string GetPhase1OperatorExtensions() => """
        # Phase 1 Operator Extensions
        ## New workflow patterns
        1. Precision width measurement:
           ImageAcquisition -> Filtering -> CaliperTool -> WidthMeasurement -> UnitConvert -> ResultJudgment -> ResultOutput
        2. AI post-processing:
           ImageAcquisition -> DeepLearning -> BoxNms -> BoxFilter -> ResultJudgment -> ResultOutput
        2.1. Detection sequence judgment:
            ImageAcquisition -> DeepLearning -> BoxNms -> DetectionSequenceJudge -> ConditionalBranch/ResultOutput
        3. Image quality gate:
           ImageAcquisition -> SharpnessEvaluation -> ConditionalBranch -> (continue or reject)
        4. Position-first inspection:
           ImageAcquisition -> ShapeMatching -> PositionCorrection(pixel-space ROI follow-up only) -> follow-up inspection
        5. Calibration-assisted metrology:
           CalibrationLoader -> CoordinateTransform/PixelToWorldTransform -> measurement operators -> UnitConvert -> ResultOutput
        ## Phrase mapping additions
        - "measure width/thickness/gap" => WidthMeasurement
        - "caliper/find edge pair" => CaliperTool
        - "point to line distance" => PointLineDistance
        - "line to line distance/parallelism" => LineLineDistance
        - "remove duplicate boxes / NMS" => BoxNms
        - "filter detections by class/area/score" => BoxFilter
        - "wire sequence / terminal order / connector order" => DetectionSequenceJudge
        - "is image sharp / focus check / blur" => SharpnessEvaluation
        - "correct ROI position / offset compensation" => PositionCorrection闂佹寧绋戦悧鍛垝鎼淬劌纾介煫鍥ㄦ礈椤﹁京绱掔仦鐐仢婵?ROI 闁荤姾娅ｉ崰鏍р枔閵忋倖鏅悘鐐跺亹閻熸繂霉閻欏懐绉柕鍡楀暣閹囧煛閸愨晛鈧偤鎮跺☉鏍у姎濞村皷鏅犻弫?        - "N-point calibration / affine calibration" => NPointCalibration
        - "load calibration file" => CalibrationLoader
        - "pixel to mm / unit conversion" => UnitConvert
        - "cycle time / elapsed statistics" => TimerStatistics
        """;

    private string GetPhase2OperatorExtensions() => """
        # Phase 2 Operator Extensions
        ## New workflow patterns
        12. Robot vision guidance:
            ImageAcquisition -> ShapeMatching -> PixelToWorldTransform/CoordinateTransform -> PointAlignment/PointCorrection -> PlcCommunication -> ResultOutput
        13. Annular part defect inspection:
            ImageAcquisition -> CircleMeasurement(center) -> PolarUnwrap -> ShadingCorrection -> SurfaceDefectDetection -> ResultOutput
        14. Traditional surface defect detection (non-AI):
            ImageAcquisition -> ShadingCorrection -> SurfaceDefectDetection -> ResultJudgment -> ResultOutput
        ## Phrase mapping additions
        - "script / custom code / formula" => ScriptOperator
        - "trigger / start / timer trigger" => TriggerModule
        - "alignment / reference point offset" => PointAlignment闂佹寧绋戦悧鍡涘磿濮樿鲸顫曢柣妯碱暯閺佸嫰姊婚崒娑欑稇妞ゆ洦鍠楅‖鍥箛閸撲胶顦?
        - "correction / compensation / send to robot" => PointCorrection闂佹寧绋戦悧鎾炽€掗崜浣瑰暫濞达絿顭堥·鍛磽閸愭儳鏋熼柣鏍电悼閳?闂佺绉寸换鎺旂矆瀹€鍕濡鑳堕悷顖炴煟閿濆懓瀚版繛鍏碱殜瀵粙宕堕鍛偖闂佺顕栧浣烘?        - "gap / pitch / lead spacing" => GapMeasurement
        - "unwrap ring / bottle cap / bearing ring" => PolarUnwrap
        - "uneven illumination / shading / flat field" => ShadingCorrection
        - "multi-frame average / temporal denoise" => FrameAveraging
        - "affine transform / rotate scale translate" => AffineTransform
        - "color measurement / deltaE / Lab" => ColorMeasurement
        - "surface defect / scratch / stain (traditional)" => SurfaceDefectDetection
        - "edge-pair defect / notch / bump" => EdgePairDefect
        - "rectangle / box / quadrilateral detection" => RectangleDetection
        - "translation-rotation calibration" => TranslationRotationCalibration
        - "hand-eye calibration / eye-in-hand / eye-to-hand" => HandEyeCalibration
        """;

    private string GetPhase3OperatorExtensions() => """
        # Phase 3 Operator Extensions
        ## New workflow patterns
        15. Large-area tiled inspection:
            ImageAcquisition -> ImageTiling -> ForEach(per-tile inspection) -> ResultJudgment -> ResultOutput
        16. Multi-view stitched inspection:
            ImageAcquisition(Image1) + ImageAcquisition(Image2) -> ImageStitching -> inspection -> ResultOutput
        17. Precision geometry chain:
            ImageAcquisition -> positioning -> GeoMeasurement(point/line/circle) -> UnitConvert -> ResultOutput
        ## Phrase mapping additions
        - "corner / vertex / corner point" => CornerDetection
        - "intersection / line crossing" => EdgeIntersection
        - "parallel lines / dual edge rails" => ParallelLineFind
        - "quadrilateral / polygon four-edge" => QuadrilateralFind
        - "geometry measurement / line-circle / circle-circle" => GeoMeasurement
        - "stitch / panorama / large image merge" => ImageStitching
        - "tiling / split grid / image blocks" => ImageTiling
        - "normalize image / standardize brightness" => ImageNormalize
        - "compose images / concat / channel merge" => ImageCompose
        - "pad border / expand image border" => CopyMakeBorder
        - "save text / export csv / save json log" => TextSave
        - "point set sort/filter/merge" => PointSetTool
        - "blob labeling / classify connected components" => BlobLabeling
        - "histogram / gray distribution" => HistogramAnalysis
        - "pixel statistics / roi mean brightness" => PixelStatistics
        """;

    private string GetRoleDefinition() => """
        # 闁荤喐鐟︾敮鐔哥珶婵犲嫧鍋撶憴鍕叝缂?

        婵炶揪绲挎慨闈浳?ClearVision 閻庤鎮堕崕鎵箔閻旂儤鍠嗛柛鈩冨嚬濞兼洘淇婇鐔蜂壕濠电偞娼欓鍛存煢閳哄懎鐭楅柟娈垮枟閻ｈ京鈧鎮堕崕鎵礊閺傛５瑙勬媴閻戞ɑ娅㈤梺鐟扮摠閸旀帞绮幘鍨涘亾绾懐鍫柍?
        婵炶揪绲挎慨宄扳枔閹寸偟顩烽悹鍥ㄥ絻椤倝鏌￠崟闈涚伈缂佸彉鍗冲鐣屾喆閸曨偆銈﹂梺娲绘娇閸斿秹宕哄☉銏″剭闁告洦鍠栧▓浼存煟閹烘挸鍔舵い鏇樺灮閹虫盯鍩€椤掑嫬绠甸煫鍥ㄨ壘閻楁岸鏌ㄥ☉妯侯殭缂侇喚濮风划璇参旈埀顒勬偤濞嗘劖鍎熼柟鎹愬蔼閸橆剟姊洪銏╂Ч閻庢哎鍔戝畷銉╁醇閵夈垹浜鹃柛灞剧矋閻ｈ京绱掗悩鎰佹當闁烩剝鐟╅弫?
        缂佺虎鍙庨崰鏍偩閸撗€鍋撻悷鏉挎殨婵犲﹥鍨块幆鍐礋椤斿墽顔曢梺瑙勪航閸庨亶宕ｈ閸栨牜鎷犻崣澶婎伅闂佸憡鐟ラ崐褰掑汲閻斿吋鐓€鐎广儱娲ㄩ弸鍌炴煥濞戞鐏遍柡浣规崌楠炲骞囬鍡╀紘婵炴垶鎼╂禍婊堟偩椤掑嫬鏋侀悗闈涙啞閻ｉ亶鎮峰▎蹇旑棥妞ゎ偄楠歌灋闁逞屽墮闇夐悗锝庝簷缁憋絽霉閿濆棛鐭嬬紒渚婄畵婵?

        ## 閻庤鎮堕崕鎵礊閺傛５瑙勬媴閸濄儱鏁ら梺鍝勭墱閸撴繈锝炴径鎰?
        1. 濠殿噯绲界换瀣煂濠婂喚鍟呴柕澶堝€楃粙濠冪箾缂堢姷顦︾紒鐑╁亾婵＄偑鍊涢濠勫垝?闂佹悶鍎查崕鎶藉磿濮橆兘鏀?缂備緡鍋夊畷鐢告偩閼姐倐鍋撳☉娆忓缂佽鲸鍨堕幈銊р偓锝呭缁€鍑ageAcquisition闂?
        2. 濠殿噯绲界换瀣煂濠婂喚鍟呴柕澶堝€楃粙濠冪箾缂堢姷顦︾紒鐑╁亾婵＄偑鍊涢濠冪?缂傚倷鐒﹂幐濠氭倶婢跺缍囬柟鎯у暱濮?缂備緡鍋夊畷鐢告偩閼姐倐鍋撳☉娆忓缂侇喓鍔戝鍫曟偆閸屾粎顦㏑esultOutput闂?
        3. 闂佸憡鐟禍娆愮箾閸ヮ剚鍋ㄩ柕濞垮€楅悷鎾绘煛閸屾碍璐￠柣锝堝吹閳ь剚绋掗崝妤€煤閹峰被浜归柡鍥ㄤ亢閸橆剟鏌涢幒鎿冩當闁搞値鍙冮幆鍐礋椤撶姵姣堥柣搴㈢⊕閸斞呮濠靛洨鈻旂€广儱鐗嗛崢鎾煕閹烘挾娲撮柍褜鍓涙慨宕囩箔婢跺备鍋撳☉娅亜锕㈤鍫熷剭闁告洦鍘鹃弳顒勬倵?
        4. 闁哄鏅濋崑鐔煎吹鎼淬劌绫嶉悹楦挎缁犳垵顪冮妶鍜佺吋濞村吋甯為埀顒傛嚀閻楀繘顢旈鍕煑闁挎繂娲╅～锕傛煕閵娿儺鍎忛柛姘儑閳ь剛顢婇～澶愬焵椤戭剙妫鎰版煕?
        5. 婵炴潙鍚嬮敋闁告ɑ绋掗幏鍛崉閵婏附娈㈤梺鍝勭墐閸嬫挾绱掗悩顐壕濠电偠寮撻懗璺衡枔閹寸姷涓嶆俊銈傚亾闁烩剝鐟х槐鎺楀礋椤愶絽鈧倝鎮楅悷鐗堟拱闁搞劍纰嶇粋鎺旀嫚閹绘帩娼?
        """;

    private string GetParameterInferenceGuide() => """
        # 闂佸憡鐟ラ崐褰掑汲閻旂厧绠抽柕濞у嫬鈧偤鏌熺粙鎸庢悙鐎?

        1. 闂佽桨鐒﹀姗€鍩€椤掆偓閸氬銇愭笟鈧畷锝夊冀椤掑缍堥梺?
        - 闂佹椿娼块崝宥夊春濞戙垹鍙婇幖杈剧岛閳ь剚顭囩槐鎺戔枎韫囨挻鐦旈梺姹囧妼鐎氼參寮抽悢鐓庣９缁绢參顥撶粈鍕攽?"闂傚倸鍟悧鍡涘焵?00"闂?闂傚倸鍊婚ˉ鎰玻?.5mm"闂佹寧绋戦ˇ顖滆姳閸欏顕辨俊顖氭惈鐢儵鏌￠崟顓炐㈤柣銊у█瀹曟岸鎮ч崼鐔剁帛闂佺儵鏅濋…鍫ュ矗瑜斿畷锝夊磼濞戞瑦顔嶉梺?
        - 婵犵鈧啿鈧綊鎮樻径鎰仺闁靛绠戦悡鏇㈡煕濮橆剚婀版俊鐐插€荤槐鎺戔枎韫囨挻鐦旈梺鐑╂櫓閸犳鎮ラ敐澶婄９闂傚倸顕悷銏ゆ倵閸︻厽鍤€婵☆垰锕弫宥夊醇濠垫劖鎼?"0.5mm闂佹寧绋戦懟顖涚缁嬫鍟呮い?.05mm"闂佹寧绋戦¨鈧紒杈ㄧ箖閹棃寮崹顔锯偓濠氭煕閹烘繂浜濇俊鎯懍鐒婇柛鏇ㄥ亜閻撳倿鏌ｉ埡濠傛灍闁绘牭缍佸畷鎰版偂鎼粹剝顥濋梺杞扮劍濠㈡﹢骞忛幍顔瑰亾閸︻厽鍤€婵☆垰锕畷锝夊磼濞戞瑦顔嶉梺?
        - 闂佸吋鐪归崕閬嶅箖閹惧墎灏甸悹鍥ㄥ絻濡﹢鏌℃担鍝勵暭婵犫偓娴ｇ懓绶炴慨姗嗗亰閸ゅ鏌涙繝鍐噰闁逞屽墮椤р偓缂佽鲸绻冪€电厧螣閸濆嫬寮楅梻渚囧亜椤︽壆鈧哎鍔戝畷銉ヮ吋閸ュ棛鍋熼幏鐘活敇濠靛牏鏋€闂佸搫鐗冮崑鎾绘煙閹帒鍔ョ紒璇查叄閹虫捇宕崘顏嗩槷濡ょ姷鍋犲▔娑橈耿?parametersNeedingReview 婵炴垶鎼╅崢浠嬫偉閿濆洦濯奸柣妤€鐗滈崝鍡椻槈閺傛妯€闁靛棗绉规俊?

        2. 闂佸憡顨嗗ú鎴犵礊閸涱喚鈻旈幖娣灩鎼村﹦绱?
        - 閻熸粎澧楅幐楣冨极閵堝绠ｉ柧姘€搁埢蹇涙煟?mm/婵炴挾鎸?缂備焦绋戦ˇ杈ㄦ櫠閸ф鍋犻柛鈩冾殔缁€瀣归敐鍛枌缂佽鲸绻堥幊鎾崇暆閳ь剟鎮鹃懡銈傚亾濞戞瑥濮囩€殿噮鍓熷顐︽偋閸啘锕傛煕瀹ュ懐绠崇紒鈧畝鍕睄閻犱礁婀辩粈澶娾槈閹惧磭孝鐟滅増鐓￠幊娑㈠幢濞戞帒浜鹃柣妯哄暱鎼村﹦绱掗悩鎰佹畷闁伙富鍠楃粭鐔衡偓锝冨妷閸?        - 闁圭厧鐡ㄥ纭呫亹娴ｈ櫣鐭嗛柧蹇氼潐閺嗗繘鏌熼幘顔芥暠鐟滈鐒︾粭鐔封槈濞嗘垵鐏遍柣搴ゎ潐閻喗绌辨繝鍥х畳妞ゆ牓鍊楃粈鍕攽?CalibrationBundleV2 / CalibrationData / CalibrationLoader 闂備焦婢樼粔鍫曟偪閸℃稒鏅鑸电〒缁€澶娾槈閹惧磭啸妞も晪绠撳畷妯侯吋閸涱剚鎹ｉ柣鐘辫兌閸嬫挸螞椤愶附鍎嶉柛鏇ㄥ墮椤や胶鈧鎮堕崕鏌ュ几閸愨晝顩烽柟顖涘閻斿懐鈧?+ 闂佸綊娼ч鍛閹邦儷鎺楀棘閸撗傜矗闁诲繐绻楀▍锝吤洪崸妤€绠抽柕濞㈢繝绀侀锝堢疀鐎Ｑ冧壕?        - 闂佸搫鐗滄禍婊堝矗閹稿骸绶為柛銉ｅ妿閸ㄥジ鎮楃憴鍕濠⒀呭█閺佸秶浠﹂挊澶庮唹闂佺绻愰悧鍛崲濮樿埖鍋╂繛鍡樺灥閳锋牠鏌ｉ悙鍙夘棞闁逞屽墲婢瑰牓顢氶姀锛勨枙濠㈣泛锕ㄧ€氭瑩鏌?parametersNeedingReview 婵炴垶鎼╅崢浠嬫偉閿濆洦濯煎ù鐓庣摠娴犳﹢鎮烽弴姘冲厡闁炽儲顭囬幏瀣Χ閸愶絽浜?
        3. 闁荤偞绋戞總鏃傜箔閻旂儤鏆滄慨婵嗙焾濞煎苯顫楀☉娆樼劸妞ゆ挸顭峰畷鎰版偂鎼粹剝顥濋梺鍏兼緲閸犳稓妲愬▎鎰浄闁告侗鍓涚粙濠囨煕韫囨梻鐭婄紒銊﹀▕閺?
        - 3C/闂佹眹鍨藉褔鎮哄▎鎰窞闁哄稁鍓﹀搴繆椤愮喎浜惧┑鐐存綑椤戞垹妲愰惂鐒梤esholding.UseOtsu=true闂佹寧绋戦悺鎼噊bAnalysis.MinArea 闂佸憡鐟崹顖滃垝?20~100 闁荤姍鍥ㄦ暠妞ゆ帞鍋ゆ俊?
        - 闂佸憡鐗曢幊鎾凰?OCR/闂佸搫顧€缁辨洟鎮ュ鍫熸櫖婵炲棛顢嘾eRecognition.MaxResults=1闂佹寧绋戦惌鍌炲焵椤掆偓閸婂摜绱炵€ｎ喖绀傞柛顐ｇ箑缁敻鏌涘Ο娆惧殭婵炲懏姊圭粙澶嬫償閳藉棗娈稿┑顕嗙稻閺屻劎鈧娅曢弲鍫曟倷閹绘帞绠掗梺?
        - 缂備緡鍠楅崕鎶芥儊閹存緷鍦偓锝庡枛濞呫倝鏌ㄥ☉娆戠叝缂侀硸鍙冨畷妤呭醇閿涱偄顦靛畷妤呭川椤旀儳鐏遍柣搴ゎ潐濮樸劑鎮鹃懡銈傚亾濞戞瑥濮х紒杈熂oordinateTransform/NPointCalibration闂佹寧绋戦¨鈧紒杈ㄧ箞閺屽棝宕归鐓庤祴闂佺儵鏅涢悺銊ф暜鐎涙ɑ缍囬柟鎯у暱濮ｅ鏌ｅ缁樻珖闁诡喖锕︽禍鎼佸传閸曢潧娈剁紓鍌欑劍閹逛線顢欓幋锕€违?
        - AI濠碘槅鍋€閸嬫挻绻涢弶鎴劀缂佽京澧楃€电厧螣閸濆嫬寮楀┑鈽嗗亐閸嬫捇鏌?ModelPath闂侀潧妫斿ù鐣乸utSize闂侀潧妫旂粩绱€nfidence 缂備焦绋戦ˇ顖炲矗瑜旈弻銊╊敊閻撳孩顥濋梺杞拌兌婢ф危閹间礁瑙﹂柨鏇楀亾缂佸墎鍠愮粋宥夊传閸曨亞鐭楃紒缁㈠弾閸犳盯顢樻繝姘?
        """;

    private string GetOperatorCatalog(string? userDescription)
    {
        // 婵?OperatorFactory 闂佸吋鍎抽崲鑼躲亹閸ヮ剙绠ラ柍褜鍓熷鍨緞婵犲倸娈ュ┑鐐差槶閸斿矂宕悾宀€涓嶆俊銈傚亾闁烩剝鐟╅幆鍐礋椤愩垹绗氶梺杞拌兌婢ф鐣垫笟鈧弫宥囦沪閽樺－妤呮煙椤戣儻鍏岄柡浣规崌楠炲骞囬鐐瘓闁诲孩绋掗崝妤€煤閹峰被浜?
        // 閻熸粎澧楅幐楣冨极閵堝绠ｉ梺鍨儏娴煎酣寮堕埡鍐暭婵″弶鍨瑰☉鐢割敊閼恒儺妲梺鎸庣☉閼活垵銇愯閳绘棃濡搁妷銉ユ辈婵°倕鍊归…鍥规径鎰鐎规洖娲ㄩ弳顒勬倵濞戞瑥濮屾い鏇熺洴楠炲棝宕崘顏嗩槷濡ょ姷鍋炲﹢鍦崲濮樿埖鍋╂繛鍡楃箰瀵潡姊洪幓鎺旂闁稿被鍔岄锝夊即濮樿京鈻曟繛?fallback
        var allMetadata = _operatorFactory
            .GetAllMetadata()
            .OrderBy(m => m.Type.ToString())
            .ToList();

        var relevantMetadata = string.IsNullOrWhiteSpace(userDescription)
            ? allMetadata
            : GetRelevantOperators(userDescription)
                .OrderBy(m => m.Type.ToString())
                .ToList();

        var sb = new StringBuilder();

        if (string.IsNullOrWhiteSpace(userDescription))
        {
            sb.AppendLine("# Full Operator Catalog");
        }
        else
        {
            sb.AppendLine("# Relevant Operator Catalog");
            sb.AppendLine("This section keeps operators most relevant to the current request.");
            sb.AppendLine("If the pruned catalog is insufficient, consult the compact fallback catalog below.");
        }

        sb.AppendLine();
        sb.AppendLine("```json");
        sb.AppendLine(SerializeOperatorCatalog(relevantMetadata, includeFullDetails: true));
        sb.AppendLine("```");

        if (!string.IsNullOrWhiteSpace(userDescription))
        {
            sb.AppendLine();
            sb.AppendLine("# Full Catalog Fallback");
            sb.AppendLine("Use this compact fallback catalog if the relevant operator subset is not enough.");
            sb.AppendLine();
            sb.AppendLine("```json");
            sb.AppendLine(SerializeOperatorCatalog(allMetadata, includeFullDetails: false));
            sb.AppendLine("```");
        }

        return sb.ToString();
    }

    private List<OperatorMetadata> GetRelevantOperators(string userDescription)
    {
        var allMetadata = _operatorFactory.GetAllMetadata().ToList();
        if (allMetadata.Count == 0 || string.IsNullOrWhiteSpace(userDescription))
            return allMetadata;

        var keywords = ExtractKeywords(userDescription);
        var matched = allMetadata
            .Where(metadata => IsRelevantByKeywords(metadata, keywords))
            .ToList();

        if (matched.Count < 8)
        {
            var categoryHints = keywords
                .Select(GetCategoryHint)
                .Where(hint => !string.IsNullOrWhiteSpace(hint))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (categoryHints.Count > 0)
            {
                matched.AddRange(allMetadata.Where(metadata =>
                    categoryHints.Any(hint => ContainsIgnoreCase(metadata.Category, hint!))));
            }
        }

        matched.AddRange(GetCoreOperators(allMetadata));

        var distinct = matched
            .GroupBy(metadata => metadata.Type)
            .Select(group => group.First())
            .ToList();

        return distinct.Count > 0 ? distinct : allMetadata;
    }

    private static HashSet<string> ExtractKeywords(string description)
    {
        var normalized = description.ToLowerInvariant();
        var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in Regex.Matches(normalized, @"[\p{L}\p{Nd}_]+"))
        {
            var token = match.Value.Trim();
            if (token.Length >= 2)
                keywords.Add(token);
        }

        AddIntentTokensIfMatched(keywords, normalized, ["measurement", "measure", "gap", "distance", "width", "caliper", "mm", "um"], ["measurement", "gap", "distance", "width", "caliper"]);
        AddIntentTokensIfMatched(keywords, normalized, ["defect", "blob", "threshold", "ng"], ["defect", "blob", "threshold"]);
        AddIntentTokensIfMatched(keywords, normalized, ["communication", "plc", "modbus", "s7", "tcp"], ["communication", "modbus", "siemens", "mitsubishi", "omron"]);
        AddIntentTokensIfMatched(keywords, normalized, ["ocr", "barcode", "recognition", "code"], ["ocr", "code", "barcode", "recognition"]);
        AddIntentTokensIfMatched(keywords, normalized, ["ai", "yolo", "deeplearning", "inference"], ["ai", "deeplearning", "inference"]);
        AddIntentTokensIfMatched(keywords, normalized, ["calibration", "undistort", "coordinate"], ["calibration", "undistort", "coordinate"]);

        return keywords;
    }

    private static void AddIntentTokensIfMatched(
        HashSet<string> keywords,
        string normalizedDescription,
        IEnumerable<string> triggers,
        IEnumerable<string> tokensToAdd)
    {
        if (!triggers.Any(trigger => normalizedDescription.Contains(trigger, StringComparison.OrdinalIgnoreCase)))
            return;

        foreach (var token in tokensToAdd)
            keywords.Add(token);
    }

    private static bool IsRelevantByKeywords(OperatorMetadata metadata, HashSet<string> keywords)
    {
        if (keywords.Count == 0)
            return false;

        if (keywords.Any(keyword =>
                ContainsIgnoreCase(metadata.DisplayName, keyword) ||
                ContainsIgnoreCase(metadata.Description, keyword) ||
                ContainsIgnoreCase(metadata.Category, keyword)))
        {
            return true;
        }

        return metadata.Keywords != null &&
               metadata.Keywords.Any(operatorKeyword =>
                   keywords.Any(keyword => ContainsIgnoreCase(operatorKeyword, keyword)));
    }

    private static bool ContainsIgnoreCase(string? source, string keyword)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(keyword))
            return false;

        return source.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetCategoryHint(string keyword)
    {
        if (keyword.Contains("measure", StringComparison.OrdinalIgnoreCase) ||
            keyword.Contains("distance", StringComparison.OrdinalIgnoreCase) ||
            keyword.Contains("width", StringComparison.OrdinalIgnoreCase))
        {
            return "measurement";
        }

        if (keyword.Contains("defect", StringComparison.OrdinalIgnoreCase) ||
            keyword.Contains("blob", StringComparison.OrdinalIgnoreCase) ||
            keyword.Contains("ng", StringComparison.OrdinalIgnoreCase))
        {
            return "defect";
        }

        if (keyword.Contains("communication", StringComparison.OrdinalIgnoreCase) ||
            keyword.Contains("plc", StringComparison.OrdinalIgnoreCase) ||
            keyword.Contains("modbus", StringComparison.OrdinalIgnoreCase))
        {
            return "communication";
        }

        if (keyword.Contains("ocr", StringComparison.OrdinalIgnoreCase) ||
            keyword.Contains("barcode", StringComparison.OrdinalIgnoreCase) ||
            keyword.Contains("recognition", StringComparison.OrdinalIgnoreCase))
        {
            return "ocr";
        }

        if (keyword.Contains("calibration", StringComparison.OrdinalIgnoreCase) ||
            keyword.Contains("undistort", StringComparison.OrdinalIgnoreCase) ||
            keyword.Contains("coordinate", StringComparison.OrdinalIgnoreCase))
        {
            return "calibration";
        }

        return null;
    }

    private static List<OperatorMetadata> GetCoreOperators(IEnumerable<OperatorMetadata> allMetadata)
    {
        var coreTypes = new HashSet<OperatorType>
        {
            OperatorType.ImageAcquisition,
            OperatorType.ResultOutput,
            OperatorType.ResultJudgment,
            OperatorType.ConditionalBranch
        };

        return allMetadata
            .Where(metadata => coreTypes.Contains(metadata.Type))
            .ToList();
    }

    private static string SerializeOperatorCatalog(IEnumerable<OperatorMetadata> metadata, bool includeFullDetails)
    {
        if (!includeFullDetails)
        {
            var fallbackCatalog = metadata.Select(m => new
            {
                operator_id = m.Type.ToString(),
                name = m.DisplayName,
                category = m.Category
            });

            return JsonSerializer.Serialize(fallbackCatalog, _catalogJsonOptions);
        }

        var detailedCatalog = metadata.Select(m => new
        {
            operator_id = m.Type.ToString(),
            name = m.DisplayName,
            category = m.Category,
            description = m.Description,
            keywords = m.Keywords ?? Array.Empty<string>(),
            inputs = m.InputPorts.Select(p => new
            {
                port_name = p.Name,
                display_name = p.DisplayName,
                data_type = p.DataType.ToString(),
                required = p.IsRequired
            }),
            outputs = m.OutputPorts.Select(p => new
            {
                port_name = p.Name,
                display_name = p.DisplayName,
                data_type = p.DataType.ToString()
            }),
            parameters = m.Parameters.Select(p => new
            {
                param_name = p.Name,
                display_name = p.DisplayName,
                type = p.DataType,
                default_value = p.DefaultValue?.ToString(),
                required = p.IsRequired,
                description = p.Description ?? string.Empty,
                min_value = p.MinValue?.ToString(),
                max_value = p.MaxValue?.ToString(),
                options = p.Options?.Select(o => new { label = o.Label, value = o.Value })
            })
        });

        return JsonSerializer.Serialize(detailedCatalog, _catalogJsonOptions);
    }

    private string GetConnectionRules() => """
        # 缂備焦妫忛崹鎷屻亹濞戞氨灏甸悹鍥皺閳ь剛鍏樺畷妤呮偂鎼搭喖鏅ｉ梺璇″劯閸♀晜缍堥梺?

        ## 闂佽桨鑳舵晶妤€鐣垫担铏瑰暗閻犲洩灏欓埀顒勵棑閹风姷鈧稒蓱椤?
        - Image闂佹寧绋掗懝鎯瑰鈧畷鎾圭疀閺冣偓濞堝爼鏌熺拠鈥虫珯缂佽鲸鐟х槐鎺楁嚄椤栨凹妫岀紓浣规閸ㄦ媽銇愬☉銏℃櫖?
        - Integer / Float闂佹寧绋掔喊宥夊汲閻旂厧纾归柤娴嬫櫈椤箓鏌涢妸銉劀缂佽鲸鐟ラ～婊冣枎閹烘埈妫岀紓浣规閸ㄦ媽銇愬☉銏℃櫖濠㈣埖绋撶粈濉坣teger 闂?Float 闂佸憡鐟崹顖涚閹烘挾顩查柟鐑樻礈缁?
        - Boolean闂佹寧绋掗懝鍓х博妞嬪簼鐒婇柡鍌氱氨閸嬫挾娑垫搴ｎ槱缂備椒鍕橀崹鍏肩珶婵犲嫮鍗氭い鏍ㄨ壘缂嶆捇鏌?
        - String闂佹寧绋掗懝楣冩偤瑜忕划顓㈡晜閼愁垼娲梺鎸庣☉閻楁劙骞楅幋锔藉殞闁肩⒈鍓︽导鍌炴煕濞嗘瑧绉剁紒?
        - Point / Rectangle闂佹寧绋掗懝楣冨吹閹寸偞濯撮柡鍥╁Ь椤箓鏌涢妸銉劀缂佽鲸鐟ч崚鎺撳緞鎼粹槅妫岀紓浣规閸ㄦ媽銇愬☉銏℃櫖濠㈣埖绋撶粈濉抩int 闂?Rectangle 闂佸憡鐟崹顖涚閹烘挾顩查柟鐑樻礈缁?
        - Contour闂佹寧绋掓穱娲偪閸℃鍤堥柟鎯х摠濞堝爼鏌熺拠鈥虫珯缂佽鲸鐟уΣ鎰邦敃閵夈儺妫岀紓浣规閸ㄦ媽銇愬☉銏℃櫖?
        - Any闂佹寧绋掗惌顕€骞戦姀銈呯疀闊洦娲濋～锕傛煕閵娿儺鍎滅紒杈ㄧ懇閹﹢骞忕仦绛嬫缂備焦妫忛崹鎷屻亹濞戙垺鏅鑸电〒缁€澶愭煕濞嗘ê鐏熷ù婊勫笚濞煎鎮欓弶鎴濐槻婵炲濮鹃濠勭礊瀹ュ洨灏甸悹鍥皺閳?

        ## 闁哄鏅濋崑鐔煎吹鎼达絿妫柨鏃囧Г鐏?
        - 婵炴垶鎸撮崑鎾斥槈閹垮啩娴风紒棰濆弮瀹曟濡疯娴煎倿鏌涘▎娆戝埌鐟滄妸鍥ㄥ殑闁兼亽鍎辨径宥夋煛閳ь剟寮甸悽娈夸紘闂佸搫顥￠妶鍥╊啎缂?
        - 婵炴垶鎸撮崑鎾斥槈閹垮啩娴风紒棰濆弮瀹曟瑩鎼圭拠鈥茬磽闂佸憡鐟辩徊鍊熴亹閸欏顩烽柕澶堝妿缁犻箖鏌熼幁鎺戝姎闁糕晛鏈鍕潩椤愶箑娈濋柡澶婄墛閹告悂宕ｉ崱娆戝崥妞ゆ牗鑹剧紞鎾绘煥濞戞澧涘褎鐗犲畷娆撴偖鐎靛摜顦?
        - 婵炴垶鎸哥粔鎾储閹寸姵濯奸柛婵勫劚缁犳岸鎮规笟顖氱伈缂佽鲸鐟ラ湁濞达綀銆€閺屻倝鐓崶褎鍤囬柕鍡楃箻瀵即顢涘☉娆戠暢闂佸憡纰嶉崹璺何涢妶澶嬪仢妞ゆ牗纰嶇粋鍫ユ煥?
        - 婵炴垶鎸哥粔鎾箖閹惧墎灏甸悹鍥皺閳ь剛鍏橀幆鍐礋椤撗傜磽闂佸憡鐟辩徊鑲╃箔婢舵劕鐭楁い鏍ㄧ箘缁犻箖鏌熼幁鎺戝姷缂佽鲸鐟╁浠嬪Χ婢跺顫￠梺绋跨箲濠€褰掓嚈閹寸偟鈻旈柍褜鍓熷顒傛喆閸曨儷?Any闂?
        """;

    private string GetOutputFormatSpec() => """
        # 闁哄鐗婇幐鎼佸吹椤撱垹鍐€闁绘挸娴风涵鈧柣鐔哥懃鐎氼垳鈧灚鐗犻弫宥夊醇濠婂啰鑸归梺鍝勭Т閵堝憡瀵奸幒鏂哄亾閻熸壆澧崇紒?

        婵炶揪绲挎慨瀵告崲閳ь剙顪冮妶鍫殭鐟滄妸鍕秶闁规儳鍟垮В澶娾槈閹绢垰浜炬繛鎴炴惄娴滄粓骞冩惔鈾€鏋栭柡鍥╁У閻?JSON 闁诲海鏁搁、濠囨寘閸曨垱鏅悘鐐跺亹閻熸繈鏌涢弽褎鍣归柟顖氱墛缁傛帞鎷嬪畷鍥┬?Markdown 婵炲濯寸徊鍧楁偉濠婂牆閿ゆ俊銈勮兌閸ㄥジ鎮规担钘夌劷闁?
        闁荤喐鐟辩紞渚€宕抽幘顔肩畱鐟滃酣寮搁崘顏佸亾濞戞瑯娈斿褏濮风槐鎾诲焵椤掑嫬绠ｉ柡宓嫬鈧數绱撻崒妤€浜鹃梺闈涙闂堟湥ON 缂傚倷鐒﹂幐濠氭倵椤栨稐绻嗛柛灞剧懅閻熸捇鏌?

        ```
        {
          "explanation": "缂備胶濮崑鎾绘偡閺囨碍绁扮悮娆撴⒑閹绘帪姊楅悹鎰枑缁傛帡鍩€椤掍胶鈻曢柛顐犲劗閸嬫挻寰勭€ｎ亶浠撮柡澶嗘櫆閻熴倗鑺遍搹鍦笉婵°倐鍋撻柣鈩冪懇瀹曨亜鐣濋崘顏嗩啎闂佽浜介崕鏌ュ蓟閻斿鍤曢煫鍥ュ劤缁€?0闁诲孩绋掗妵鐐寸閹烘绀冮柛婊冨暟缁€?,
          "operators": [
            {
              "tempId": "op_1",
              "operatorType": "ImageAcquisition",
              "displayName": "闂佹悶鍎查崕鎶藉磿濮樿埖鐓傞柛銉墯閼?,
              "parameters": {
                "sourceType": "camera",
                "triggerMode": "Software"
              }
            }
          ],
          "connections": [
            {
              "sourceTempId": "op_1",
              "sourcePortName": "Image",
              "targetTempId": "op_2",
              "targetPortName": "Image"
            }
          ],
          "parametersNeedingReview": {
            "op_3": ["ModelPath", "Confidence"]
          },
          "recommendedTemplate": {
            "templateId": "template-guid-or-empty",
            "templateName": "缂備焦妫忛崹浼存偤濞嗘垹妫柛顭戝枤绾板秵淇婇鐔蜂壕濠?,
            "matchReason": "闂佸憡绋掗崹婵嬫嚈閹达箑绀傞柟鎯板Г閺嗘盯鎮归崶褏鈻岀紒杈ㄥ閻ヮ亪宕归鍓ь暡闂侀潧妫旈懗鍫曨敂椤掑倵鍋撳☉娆忓闁逞屽厸閻掞妇鏁€靛摜妫柛褎銇滈埀顒€瀛╅幆?,
            "matchMode": "template-first",
            "confidence": 0.92
          },
          "pendingParameters": [
            {
              "operatorId": "op_3",
              "parameterNames": ["ModelPath", "Confidence", "TargetClasses"]
            }
          ],
          "missingResources": [
            {
              "resourceType": "Model",
              "resourceKey": "DeepLearning.ModelPath",
              "description": "缂傚倸鍊搁幖顐︽儍椤栫偛鐭楁い鏍ㄧ矋閺嗗繑淇婇妞诲亾瀹曞洠鍋撻悜钘夋闁搞儻闄勯浠嬫偣娓氼垰鐏犵紒?
            }
          ]
        }
        ```

        > `recommendedTemplate` / `pendingParameters` / `missingResources` 婵炴垶鎸搁幖顐ャ亹閺屻儲鐒诲璺猴功閹界喐绻涢崼銏犱化闁?
        > 闂佸吋鐪归崕鎻掞耿椤撱垹宸濋柟瀛樺笩閸橆剙螖閸屾冻鑰挎い锝堟硾铻ｉ柍鈺佸暞缁舵煡鏌涢敂鎯у妺婵炲懏鐟╅弫宥囦沪閽樺顔夐柡澶婄墛閹告悂宕甸鐘电煔闁告繂瀚烽崵鐘绘偣閻愨晛澧查柛銊ｅ妿缁岸鎮滃Ο缁橆啀缂傚倷绀佺€氥劑鍩€?

        ## 闂佺绻愰崢鏍姳椤掑嫬鐭楅柛灞剧⊕濞堝爼姊洪弶璺ㄐら柣?(parameters)
        - 闂佸搫绉烽～澶婄暤娓氣偓閹粙濡搁敃鈧悡鏇㈡煟閵娿儱顏€殿喗瀵у濠氭偋閸繆鍚傞梺鍝勫亞閸樺ジ骞冩惔銊﹀仩闁糕剝鐟﹂悾閬嶆煕濞嗗繐鈧綊寮抽悢鐓庣９缂佹銆€閸嬫捇宕掑鍏兼悙闂佸搫顑嗙划宥咁嚕閹稿孩浜ゅΛ棰佹祰閸橆剟鏌涢弽褎鍣归柟顖氱墦瀵偅瀵奸弶鎴烆仭闂侀潧妫旂粈渚€寮伴崒婧惧亾閸偅鍋犻柍褜鍏涚粈渚€濡甸崶鈺€鐒婇煫鍥ㄦ礈閹肩厧菐閸ワ絽澧插ù鐓庢嚇閺佸秶浠﹂悾灞界暔闁哄鍎愰崜姘暦閸欏鈻旈柧蹇氼潐缁佹煡骞栫€涙ɑ鈷掓繛鍫熷灴瀵偊寮舵惔鎾充壕?
        - 闂佽桨鐒﹀姗€鍩€椤掑啫浜归悶姘煎亰瀹曞湱鈧綆浜滃Λ姗€鏌℃担鐟邦棆婵?value 闂婎偄娲ら幊姗€濡磋箛娑樻嵍闁靛鍎洪崵鐘诲箹鐎涙ɑ鈷掗柣锝堝吹閳ь剚绋掗崝鎺撶┍婵犲洤绠掓い鏍ㄧ矋閻?`min_value` 闂?`max_value` 婵炴垶鏌ㄩ澶娢?
        - 闂佸搫顑嗛惌顔戒繆?enum)缂備緡鍋夐褔鎮楅悜钘夌煑闁稿本绋掑▓鍫曟煟?value 闂婎偄娲ら幊姗€濡磋箛娑樺強妞ゆ牗鍑归崵鐘诲箹鐎涙ɑ鈷掗柣锝堝吹閳ь剚绋掗崝鎺撶┍婵犲洤绠掓い鏍ㄧ矋閻?`options` 闂佽桨鐒︽竟鍡欏垝瀹ュ棛鈻旀い鎾跺仜閻忔瑩鏌涢幋锝嗩仩婵?`value` 婵炴垶鏌ㄩ鍕博?
        - 婵炴垶鎹佸銊ц姳閿熺姴绀傞柣鎾冲瘨閸熷洤鈽夐幘宕囆㈤柟顔芥尭铻ｉ柍銉ョ－閳ь剛鍏橀弫宥団偓鐟版疆arameters` 婵炴垶鎼╅崢鎯р枔閹达箑纾归梻鍌氼嚟閸犳﹢鏌涜箛鎾跺濠电偛娲幃浠嬪Ω閵壯勬喕缂備焦顨愮紓姘辨啺閸℃稒鏅繛鎴烇供濞层倝鏌＄€ｎ偆鐭庣紒棰濆弮瀹曟瑦娼幍顔兼櫓 JSON 闂佸搫绉村ú顓㈠闯濞差亝鏅柛顐ゅ枑濞堝爼鎮?闁汇埄鍨伴崯顐︽儍閻㈠憡鏅鑸电〒缁€澶岀磼椤栨繂鍚圭紒顔惧劋缁嬪﹪鎮㈤崜浣虹崶闁诲繐绻嬬划娆撳闯濞差亜绀傞柣鎾冲瘨閸熷洭鎮峰▎娆戠ɑ闁?
        - 闂佸搫鐗滄禍婊冿耿?user 闂佸湱绮崝妤呭Φ濮橆厾鈻旀い鎾跺枑椤牜鐥褍鏋熼柛銊ｅ姂瀵噣鎮╁畷鍥モ偓濠囨煙閹帒濡介柡灞芥喘閹啴宕熼銏☆棟闂佽桨绀佹惔婊呮濠靛洣绻嗛柛灞剧〒娴滎垶鎮楅悷鏉挎殲婵犫偓?`default_value`闂佹寧绋戦懟顖濄亹閸欏顩烽柕澹嫰妲ｉ梺浼欑祷閸庢煡宕归妸锔剧畽妞ゆ劑鍨瑰鍐差潡濞戞瑯鐒炬い鎾愁煼瀹?

        ## 闂佺绻愰崢鏍姳?parametersNeedingReview
        闂佸憡甯楅〃鍛村吹椤撶喐濯撮柣妯挎珪閿熴儲绻涙径瀣；缂侇喚濞€閹粙濡搁敃鈧悡鏇㈡煙鐠囪尙绠洪柛顐簼缁嬪顢橀悩顐熷亾濡皷鍋撶憴鍕暡婵″弶鍨瑰☉鐢割敊鐏忔牕浜剧紒妤勩€€閸嬫挻鎷呴崫銉х暠婵＄偑鍊涘畷鐢稿极闁秵鍋ㄩ柕濠忕畱閻撴洟鏌熺紒妯哄缂佸墎鏁婚幆鍥偄妞嬪孩婢栭梺缁樼矤閸ㄤ即銆傞妸鈺佹瀬闁绘鐗嗙粊锕傛煟閵娿儱顏╃€殿噮鍓熷顐︽嚋椤戣棄浜?
        婵炴挻鑹鹃鍛淬€呰閺佸秴顫濋鐔衡偓顔济归悩鍐插姸闁活厽鍎抽銉╁礋閳规儳浜惧☉鎿冩憠 闂侀潻闄勫妯侯焽閸愵喖违濞达綁绱︽笟鈧畷鍦偓锝庡亝閻庮喖霉閻樺啿鍔堕柣顓熷劤椤曘儵宕熼埞鎯т壕濞达絽鎼ˉ妤呮倵鐟欏嫯澹橀柡鍕€婚埀顒€鐏氶幃鍌毼ｉ崶顒€纾归柤娴嬫櫇閹煎ジ鏌?

        ## 闂佺绻愰崢鏍姳?pendingParameters
        - 婵?`parametersNeedingReview` 婵烇絽娲︾换鍐偓鍨⒒閹风娀顢樺┑鍫㈡瀫婵炴垶鎸撮崑鎾绘煠閻ゎ垱褰х紒杈ㄧ箖閹峰懘宕卞▎鎴炲皾闂佸搫顑呯€氼厼煤閸ф鐒婚柛灞剧閸娿倝鏌涢幘宕囆ゆい蹇ｅ墴閹嫮鈧稒锚婢跺秵绻涢幘鍐茬骇闁绘挸顑夋俊?
        - 濠殿噯绲界换姗€濡村澶婄闁告侗鍘介崕?`operatorId` 闂佸憡绮岄惌鍌氥€掗崜浣瑰暫濞达綀銆€閳ь剚顭囬幏瀣Χ閸ャ劎鏆?`parameterNames`闂?

        ## 闂佺绻愰崢鏍姳?missingResources
        - 闂佹椿娼块崝瀣姳椤掑倹鍋橀柕濞垮妽瑜把勪繆椤栨澧叉繝銏★耿閹峰啴鎸婃径瀣瑎缂傚倸鍊搁幖顐ャ亹濞戙垺鏅悘鐐电摂濞层倖淇婇妞诲亾瀹曞洠鍋撻悜钘夋闁搞儻闄勯浠嬫煏閸℃洜鍔嶉柣鏍电悼缁敻鎮欓浣衡偓顔济归悩铏瑰牚闁逞屽厸鐠у┌C 闂侀潻闄勫妯侯焽閸愵喖违濞达絽婀遍崹濂告倵鐟欏嫮顣查柡瀣暞缁傛帡宕滄担鐑樻儯闂?
        - 濠殿噯绲界换姗€濡村澶婄闁告侗鍘介崕?`resourceType` / `resourceKey` / `description`闂?

        ## 闂佺绻愰崢鏍姳?recommendedTemplate
        - 婵°倕鍊归敃銏ゃ€傞崼鏇炴嵍闁绘垶蓱閻濐垶鏌ㄥ☉妯煎妞も敪鍛／闁割煈鍠氱喊?缂備焦妫忛崹浼存偤濞嗘挻鏅璺猴功鐎瑰霉閸忔祹顏堝储濞戞氨纾兼繛鍡楃箰濮ｅ淇婇妤€澧叉繝銏★耿楠炴帡濡搁妸銉ь槰婵烇絽娲犻崜婵囧閸涙潙违?
        - `matchMode` 闂佽浜介崝蹇撶暦濮椻偓瀹曞爼宕崟顓熸瘞婵?`template-first`闂?

        ## 闂佺绻愰崢鏍姳?tempId
        闂佸搫绉堕崢褏妲愰敓鐘茬倞闁告繂瀚弳鏉库槈?op_1, op_2, op_3...闂佹寧绋戦張顒傗偓鍨矌缁螖閳ь剟鎮哄▎鎾虫嵍闁靛濡囬妶锔剧磼鐎ｎ亶鍎庨柤鍨灴閹啴宕熼鈧埛鏃堟偠濞戞鐒搁柕鍡楀閹棁绠涘☉鎺戜壕闁圭儤鍩堥弶濠氭煏?

        ## 闂佺绻愰崢鏍姳?operatorType
        闂婎偄娲ら幊姗€濡磋箛鏃傗枖閹艰揪绲块弳顒勬倵濞戞瑥濮夋繛鍙夊閵囨劙寮村鍐插箑闂?operator_id 闁诲孩绋掗〃鍡涱敊瀹€鈧埀顒傛嚀閼活垶宕ｈ箛鏃傗枖闁逞屽墴閹虫稒娼忛崜褏顦╂繝銏犵垻閸愵亝鐦撻梺鍛婂姈閻燂箓寮查柆宥呯疀闁绘洖鍊荤粈鍡涙煏?

        ## 闂佺绻愰崢鏍姳椤掑倻鍗氭い鏍ㄨ壘缂嶆捇鏌?
        闂婎偄娲ら幊姗€濡磋箛鏃傗枖閹艰揪绲块弳顒勬倵濞戞瑥濮夋繛鍙夊閵囨劙寮村鍐插箑闂?port_name 闁诲孩绋掗〃鍡涱敊瀹€鈧埀顒傛嚀閼活垶宕ｈ箛鏃傗枖闁逞屽墴閹虫稒娼忛崜褏顦╂繝銏犵垻閸愵亝鐦撻梺鍛婂姈閻燂箓寮查柆宥呯疀闁绘洖鍊荤粈鍡涙煏?
        """;

    private string GetFewShotExamples() => """
        # 缂備讲鍋撻弶鐐村娴兼劙鏌ㄥ☉妯煎妞ゆ帞鍠愮粙濠囨偐閾忓湱顔愭繛瀛樼矋缁嬫捇濡靛顓犵懝閻庯綆鍓氶悾閬嶆煛瀹ュ洤甯剁紒鎲嬬節瀹曨亞浠﹂柨顖氫壕婵犻潧锕﹂悢鍛存煥?

        ## 缂備讲鍋撻弶鐐村娴?1
        闂佹椿娼块崝宥夊春濞戙垹绠甸煫鍥ㄨ壘閻楁岸鏌?濠碘槅鍋€閸嬫挻绻涢弶鎴剮濡ょ姴娲畷顐ｆ媴婵劏鍋撻崘顔筋棃闁靛繈鍨圭换渚€姊婚崟顐㈡缂佽鲸绻堥幃浠嬪Ω瑜庣粊鏌ユ煛閸垹鍔嬪┑顖氭健閹ゎ槻闁诡喗顨婂畷姘跺幢濡皷鍋?

        濠殿喗绻愮徊鍧楀灳濡粯缍囬柟鎯у暱濮ｅ鏌?
        {
          "explanation": "闂佺儵鏅涢幉鈥斥攦閳ь剟姊洪幓鎺撳枠婵炲绠撳畷鍫曞箚瑜嶉崜濂告煥濞戞鐒告い锝傛櫆瀵板嫰宕熼鐔封偓鐐烘⒒閸曨偆孝婵炲懏妫冮弫宥囦沪閼测晝妾堕梺绋匡功閸樠呪偓鍨叀瀹曟岸宕卞▎妯尖偓鑼磽閸屾稒銇濇繛鍜冪節閺佸秶鈧稑顩畂b闂佸憡甯掑Λ娆撴倵閻ｅ瞼纾奸柣鏃€妞块崥鈧紓鍌氬€瑰钘夆枍閿熺姴鏋佸ù鍏兼綑濞呫倝鏌ㄥ☉妯绘拱婵炴挸澧庣槐鎺楀醇閿濆洨鐐曢梺鍛婂灱濞咃絿鍒掗妸鈺佸嚑?,
          "operators": [
            {"tempId": "op_1", "operatorType": "ImageAcquisition", "displayName": "闂佺儵鏅涢幉鈥斥攦閳ь剟姊洪幓鎺撳枠婵?, "parameters": {"SourceType": "Camera", "TriggerMode": "Software"}},
            {"tempId": "op_2", "operatorType": "Filtering", "displayName": "婵°倕鍊硅摫闁哄苯顦…銊╁Χ閸℃姣?, "parameters": {"KernelSize": "5"}},
            {"tempId": "op_3", "operatorType": "Thresholding", "displayName": "婵炲瓨绮岄懟顖炲焵椤掑倸甯堕悗?, "parameters": {"UseOtsu": "true"}},
            {"tempId": "op_4", "operatorType": "BlobAnalysis", "displayName": "缂傚倸鍊瑰钘夆枍椤″潤ob闂佸憡甯掑Λ娆撴倵?, "parameters": {"MinArea": "50", "MaxArea": "5000"}},
            {"tempId": "op_5", "operatorType": "ResultOutput", "displayName": "濠碘槅鍋€閸嬫挻绻涢弶鎴剳缂侇喓鍔戝?, "parameters": {"Format": "JSON", "SaveToFile": "false"}}
          ],
          "connections": [
            {"sourceTempId": "op_1", "sourcePortName": "Image", "targetTempId": "op_2", "targetPortName": "Image"},
            {"sourceTempId": "op_2", "sourcePortName": "Image", "targetTempId": "op_3", "targetPortName": "Image"},
            {"sourceTempId": "op_3", "sourcePortName": "Image", "targetTempId": "op_4", "targetPortName": "Image"},
            {"sourceTempId": "op_4", "sourcePortName": "BlobCount", "targetTempId": "op_5", "targetPortName": "Result"}
          ],
          "parametersNeedingReview": {
            "op_4": ["MinArea", "MaxArea"]
          }
        }

        ## 缂備讲鍋撻弶鐐村娴?2
        闂佹椿娼块崝宥夊春濞戙垹绠甸煫鍥ㄨ壘閻楁岸鏌?闂佽顔栭崑鍛嚕閹稿海顩茬憸宥夊箹瑜庣粋宥呯暆閳ь剙菐椤曗偓閹秵绗熸繝鍕槷闂備緡鍋呮穱铏规崲閸嶅dbus闂佸憡鐟﹂崹鍦垝閻ф┆C闂佹寧绋戦懟顖炴嚐閻斿憡缍囬柟鎯у暱濮ｅ姊洪锝囩叝濞ｅ洤锕︾槐鎺楀箻鐎甸晲鍑?

        濠殿喗绻愮徊鍧楀灳濡粯缍囬柟鎯у暱濮ｅ鏌?
        {
          "explanation": "闂佺儵鏅涢幉鈥斥攦閳ь剟姊洪幓鎺撳枠婵炲绠撳畷鍫曞箚瑜嶉崜濂告煥濞戞ɑ婀版繛纰卞灦閹秵鎷呴悾灞绢潥闂佸憡甯╅崑鍛般亹娓氣偓瀹曪綁寮介妸锔锯偓顕€鏌￠崼顐㈠⒕缂佽鲸绻堥弻鍛潩瀹曞洨鐣?Modbus TCP 闂佸憡鍔栭悷銉╁矗?PLC闂佹寧绋戦懟顖炴嚐閻旂厧绠┑鐘叉礌閸嬫挸顫濋锛勭畾闂佸憡绻傜粔瀵歌姳閹绘崼褔宕堕妸銏犱壕闁哄嫬绻掔紙濠氭煕?,
          "operators": [
            {"tempId": "op_1", "operatorType": "ImageAcquisition", "displayName": "闂佺儵鏅涢幉鈥斥攦閳ь剟姊洪幓鎺撳枠婵?, "parameters": {"SourceType": "Camera", "TriggerMode": "Software"}},
            {"tempId": "op_2", "operatorType": "CodeRecognition", "displayName": "婵炲瓨绮岄惉鐓幥庨鈧幆宥嗘媴閻ｅ本顫氶梺?, "parameters": {"CodeType": "QR", "MaxResults": "1"}},
            {"tempId": "op_3", "operatorType": "ModbusCommunication", "displayName": "Modbus闂佸憡鐟﹂崹鍧楀焵?, "parameters": {"Protocol": "TCP", "Port": "502", "FunctionCode": "WriteMultiple"}},
            {"tempId": "op_4", "operatorType": "ResultOutput", "displayName": "闁哄鐗婇幐鎼佸吹椤撱垺鐒绘慨姗嗗亗缁诲棛绱撴担瑙勫鞍闁?, "parameters": {"Format": "JSON", "SaveToFile": "false"}}
          ],
          "connections": [
            {"sourceTempId": "op_1", "sourcePortName": "Image", "targetTempId": "op_2", "targetPortName": "Image"},
            {"sourceTempId": "op_2", "sourcePortName": "Text", "targetTempId": "op_3", "targetPortName": "Data"},
            {"sourceTempId": "op_3", "sourcePortName": "Response", "targetTempId": "op_4", "targetPortName": "Text"}
          ],
          "parametersNeedingReview": {
            "op_3": ["IpAddress", "SlaveId", "RegisterAddress"]
          }
        }

        ## 缂備讲鍋撻弶鐐村娴?3
        闂佹椿娼块崝宥夊春濞戙垹绠甸煫鍥ㄨ壘閻楁岸鏌?闂佺儵鎳炴繝顓熶繆椤栨せ鍋撳畷鍥ｅ亾瀹勬嫈娑㈠焵椤掆偓闇夐悗锝傛櫊閻涙捇鏌涘┑鍛板厡鐎规悂浜跺浠嬪礄閵堝洨顦┑鐐茬焿缁辨洖顔忛柆宥嗏挃鐎瑰嫭婢樼徊绡汯婵烇絽娲犻埀顒€鍟挎繛鍥煥濞戞ɑ婀版繝鈧担铏圭＝婵炲樊浜濋鐐烘煕濞嗘劕骞怗婵烇絽娲犻埀顒€鍟挎繛鍥╃磽娴ｅ厜鏀癓C"

        濠殿喗绻愮徊鍧楀灳濡粯缍囬柟鎯у暱濮ｅ鏌?
        {
          "explanation": "闂佺儵鏅涢幉鈥斥攦閳ь剟姊洪幓鎺撳枠婵炲绠撳畷銉︽償閿濆洨绀嗛梺琛″亾闁硅鍔曞▓?AI 闁哄鐗婇幐鎼佸矗閸℃瑤鐒婇柛婵嗗閸ょ喖鏌ㄥ☉妯绘拱缂佽鐒﹂幆鏃堟晝閳ь剟顢楅悢鍝モ枙闁绘ê鍟块懙褰掓煟閻愬弶顥＄紒棰濆弮瀹曟瑩鎼归悷鎵畳闂傚倸瀚伴弨閬嶅汲閻斿吋鐓傞煫鍥ュ劤缁€澶岀磽娴ｈ灏伴柣蹇擃樀瀹曟岸濡堕崨顖涙瘞缂傚倸鍊瑰钘夆枍閿熺姴鏋侀柣妤€鐗婄瑧闂佸憡鐔紓姘辨嫻?0闂佹寧绋戦懟顖炲疮閳ь剟姊洪锝勪孩缂佽鍟村鍫曞灳閸欏鍋ㄩ梺鍛婂笒濡瑩寮鈧畷姘跺幢濡も偓閻掑ジ鏌涘▎鎰伌闁?OK/NG 婵烇絽娲犻埀顒€鍟挎繛鍥煥濞戞瀚伴柤鑽ゅ枑濞煎繘骞橀崘鍙夌様闂佸搫鐗冮崑鎾剁磽娴ｅ摜澧曢柛銊ф櫕閳?,
          "operators": [
            {"tempId": "op_1", "operatorType": "ImageAcquisition", "displayName": "闂佺儵鏅涢幉鈥斥攦閳ь剟姊洪幓鎺撳枠婵?, "parameters": {"SourceType": "Camera", "TriggerMode": "Software"}},
            {"tempId": "op_2", "operatorType": "ImageResize", "displayName": "闂佹悶鍎查崕鎶藉磿濮樿京纾介柍鍝勫€归弶?, "parameters": {"Width": "640", "Height": "640"}},
            {"tempId": "op_3", "operatorType": "DeepLearning", "displayName": "AI缂傚倸鍊瑰钘夆枍閳ユ緞娑㈠焵椤掆偓闇?, "parameters": {"Confidence": "0.5", "UseGpu": "true"}},
            {"tempId": "op_4", "operatorType": "ResultJudgment", "displayName": "缂傚倸鍊瑰钘夆枍閿熺姴绀嗛柕鍫濇噽閺?, "parameters": {"Condition": "Equal", "ExpectValue": "0"}},
            {"tempId": "op_5", "operatorType": "ConditionalBranch", "displayName": "OK/NG闂佸憡甯掑Λ娆撳极?, "parameters": {"Condition": "Equal", "CompareValue": "true"}},
            {"tempId": "op_6", "operatorType": "ModbusCommunication", "displayName": "闂佸憡鐟﹂崹鍧楀焵椤戝灝澹嘖", "parameters": {"FunctionCode": "WriteSingle", "RegisterAddress": "1", "WriteValue": "1"}},
            {"tempId": "op_7", "operatorType": "ModbusCommunication", "displayName": "闂佸憡鐟﹂崹鍧楀焵椤戣儻鍘獹", "parameters": {"FunctionCode": "WriteSingle", "RegisterAddress": "1", "WriteValue": "0"}},
            {"tempId": "op_8", "operatorType": "ResultOutput", "displayName": "濠碘槅鍋€閸嬫挻绻涢弶鎴剳缂侇喓鍔戝?, "parameters": {"Format": "JSON", "SaveToFile": "false"}}
          ],
          "connections": [
            {"sourceTempId": "op_1", "sourcePortName": "Image", "targetTempId": "op_2", "targetPortName": "Image"},
            {"sourceTempId": "op_2", "sourcePortName": "Image", "targetTempId": "op_3", "targetPortName": "Image"},
            {"sourceTempId": "op_3", "sourcePortName": "DefectCount", "targetTempId": "op_4", "targetPortName": "Value"},
            {"sourceTempId": "op_4", "sourcePortName": "IsOk", "targetTempId": "op_5", "targetPortName": "Value"},
            {"sourceTempId": "op_5", "sourcePortName": "True", "targetTempId": "op_6", "targetPortName": "Data"},
            {"sourceTempId": "op_5", "sourcePortName": "False", "targetTempId": "op_7", "targetPortName": "Data"},
            {"sourceTempId": "op_4", "sourcePortName": "IsOk", "targetTempId": "op_8", "targetPortName": "Result"}
          ],
          "parametersNeedingReview": {
            "op_3": ["ModelPath", "InputSize", "TargetClasses"],
            "op_6": ["IpAddress", "Port", "SlaveId"],
            "op_7": ["IpAddress", "Port", "SlaveId"]
          }
        }

        ## 缂備讲鍋撻弶鐐村娴?4
        Example 4
        User request: "convert pixel point (120,160) to millimeter coordinates and output it"

        Expected output:
        {
          "explanation": "Capture the image, do lightweight preprocessing, load a CalibrationBundleV2, then use CoordinateTransform to convert the specified pixel point into physical coordinates and output the result.",
          "operators": [
            {"tempId": "op_1", "operatorType": "ImageAcquisition", "displayName": "Image Acquisition", "parameters": {"SourceType": "Camera", "ExposureTime": "10000"}},
            {"tempId": "op_2", "operatorType": "Filtering", "displayName": "Gaussian Filter", "parameters": {"KernelSize": "5"}},
            {"tempId": "op_3", "operatorType": "CalibrationLoader", "displayName": "Calibration Loader", "parameters": {"FilePath": "calibration_bundle_v2.json"}},
            {"tempId": "op_4", "operatorType": "CoordinateTransform", "displayName": "Coordinate Transform", "parameters": {"PixelX": "120", "PixelY": "160"}},
            {"tempId": "op_5", "operatorType": "ResultOutput", "displayName": "Result Output", "parameters": {"Format": "JSON", "SaveToFile": "false"}}
          ],
          "connections": [
            {"sourceTempId": "op_2", "sourcePortName": "Image", "targetTempId": "op_4", "targetPortName": "Image"},
            {"sourceTempId": "op_3", "sourcePortName": "CalibrationData", "targetTempId": "op_4", "targetPortName": "CalibrationData"},
            {"sourceTempId": "op_4", "sourcePortName": "PhysicalX", "targetTempId": "op_5", "targetPortName": "Result"}
          ],
          "parametersNeedingReview": {
            "op_3": ["FilePath"],
            "op_4": ["PixelX", "PixelY"]
          }
        }

        ## 缂備讲鍋撻弶鐐村娴?5
        闂佹椿娼块崝宥夊春濞戙垹绠甸煫鍥ㄨ壘閻楁岸鏌?濠碘槅鍋€閸嬫挻绻涢弶鎴剳缂侇喓鍔戝绋款煥閸愩劍娅㈤梺绋跨箞閸庢煡寮抽悢鐓庣妞ゆ棁鍋愬銊╂煥濞戞瀚伴柤鑽ゅ枑濞煎繘骞橀崘鍙夌様闁荤姳鐒﹀妯肩礊瀹€绫?

        濠殿喗绻愮徊鍧楀灳濡粯缍囬柟鎯у暱濮ｅ鏌?
        {
          "explanation": "闂佺儵鏅涢幉鈥斥攦閳ь剟姊洪幓鎺撳枠婵炲绠撳畷銉︽償閵堝洨顔掗柣鐐寸☉婵傛梻鑺遍埡鍛９闁绘挸楠搁褔鏌?Blob 闂佸憡甯掑Λ娆撴倵娴犲鏅€光偓閳ь剛鍒掗妸鈺佸嚑婵犲﹤鍟悘鏌ユ倵鐟欏嫯澹橀柟顔筋殘娴滄悂宕卞Δ鈧悘鏌ユ倵鐟欏嫭鐨戠紒顔哄姂瀵顭ㄩ崘銊︽闂佺绻堥崕鏌ュ汲閻旂厧绠叉い鏃囧亹濮樸劑鏌ㄥ☉妯垮闁艰崵鍠栭獮搴㈢節閸滀礁鏁归悷?ID 闁哄鐗婇幐鎼佸吹椤撶姷纾兼繛鍡楃箰椤ゅ懐绱掗弮鎴濈仭闁搞劊鍔嶇粙澶嬬節閸愵亜鐓戠紓渚囧灥瀹曠數鍒?,
          "operators": [
            {"tempId": "op_1", "operatorType": "ImageAcquisition", "displayName": "闂佺儵鏅涢幉鈥斥攦閳ь剟姊洪幓鎺撳枠婵?, "parameters": {"SourceType": "Camera", "TriggerMode": "Software"}},
            {"tempId": "op_2", "operatorType": "Thresholding", "displayName": "婵炲瓨绮岄懟顖炲焵椤掑倸甯堕悗?, "parameters": {"UseOtsu": "true"}},
            {"tempId": "op_3", "operatorType": "BlobAnalysis", "displayName": "缂傚倸鍊瑰钘夆枍閿熺姴绀嗛柛鈩冾焽閳?, "parameters": {"MinArea": "100", "MaxArea": "5000"}},
            {"tempId": "op_4", "operatorType": "ResultJudgment", "displayName": "OK/NG闂佸憡甯囬崐鏇㈡偩?, "parameters": {"Condition": "Equal", "ExpectValue": "0"}},
            {"tempId": "op_5", "operatorType": "DatabaseWrite", "displayName": "闁荤姳鐒﹀妯肩礊瀹ュ绀嗛柣妤€鐗婂▓鍫曟煙鐠団€虫灈缂?, "parameters": {"DbType": "SQLite", "TableName": "InspectionResults"}},
            {"tempId": "op_6", "operatorType": "ResultOutput", "displayName": "闁哄鐗婇幐鎼佸吹椤撶姷纾奸柟鎯ь嚟娴?, "parameters": {"Format": "JSON", "SaveToFile": "false"}}
          ],
          "connections": [
            {"sourceTempId": "op_1", "sourcePortName": "Image", "targetTempId": "op_2", "targetPortName": "Image"},
            {"sourceTempId": "op_2", "sourcePortName": "Image", "targetTempId": "op_3", "targetPortName": "Image"},
            {"sourceTempId": "op_3", "sourcePortName": "BlobCount", "targetTempId": "op_4", "targetPortName": "Value"},
            {"sourceTempId": "op_4", "sourcePortName": "JudgmentValue", "targetTempId": "op_5", "targetPortName": "Data"},
            {"sourceTempId": "op_5", "sourcePortName": "RecordId", "targetTempId": "op_6", "targetPortName": "Text"}
          ],
          "parametersNeedingReview": {
            "op_5": ["ConnectionString", "TableName"]
          }
        }
        """;
}
