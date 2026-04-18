/// <summary>
/// 블로킹 컷씬·JSON 리소스 파일명과 1:1 대응하는 키.
/// Inspector 직렬화(int) 호환을 위해 멤버 순서·이름 변경 금지.
/// </summary>
public enum StoryKey // JSON: Resources/StoryData/&lt;이름&gt;.json
{
    None,

    // ── 블로킹 컷씬 ────────────────────────────────────────────────────────
    Chapter1_Opening,           // 1장 전체 (스킵 불가) — Resources: StoryData/Chapter1_Opening
    Chapter2_Safe,              // 2장 금고
    Chapter3_Decision,          // 3장 결심 (탈출 직전)
    Chapter4_PreBoss,           // 4장 하운드 보스 전
    Chapter4_PostBoss,          // 4장 하운드 보스 후
    Chapter5_Captive,           // 5장 감금
    Chapter6_PreBoss,           // 6장 서류 보스 전
    Chapter6_PostBoss,          // 6장 서류 보스 후
    Chapter7_BadgeDiscovery,    // 7장 견장 발견
    Chapter8_Helicopter,        // 8장 헬기 장면
    Chapter8_PostFight,         // 8장 전투 후 출발
    Chapter9_Mechanic,          // 9장 야장
    Chapter10_PreBoss,          // 10장 형 보스 전
    Chapter10_Reveal,           // 10장 폭로 + 현·연서 대화
    Chapter11_Collapse,         // 11장 현 쓰러짐 (Shadow 보스 전)
    Chapter11_Acceptance,       // 11장 그림자와 화해 (Shadow 보스 내면)
    Chapter11_Awakening,        // 11장 각성 (Shadow 보스 후)
    Chapter12_Opening,          // 12장 설계자 옥상 오프닝
}
