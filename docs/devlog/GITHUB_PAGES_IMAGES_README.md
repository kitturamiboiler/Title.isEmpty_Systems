# GitHub Pages (`*.github.io`) 이미지 넣는 방법

Base64는 HTML이 비대해지고 diff·캐시·에디터가 모두 불편하므로 **파일로 올리고 상대 경로**를 쓴다.

## 1. 폴더 구조 (저장소 루트 기준)

`umbrella_all_in_one.html`이 **루트**에 있다고 가정:

```text
kitturamiboiler.github.io/
├── index.html
├── umbrella_all_in_one.html   (또는 이 안에 devlog 섹션)
└── images/
    └── 2026-04-18/
        ├── 01-opening-narration.png
        ├── 02-onomatopoeia.png
        └── 03-unicode-quotes.png
```

HTML 안에서는:

```html
<img src="images/2026-04-18/01-opening-narration.png" alt="..." />
```

## 2. 이 Unity 프로젝트에서 복사할 파일

원본 PNG는 여기에 있다:

`docs/devlog/images/2026-04-18/*.png`

→ GitHub Pages 저장소의 `images/2026-04-18/`로 그대로 복사하면 된다.

## 3. HTML이 하위 폴더에만 있을 때

예: `logs/2026-04-18.html` 이면 경로를 한 단계 올린다:

```html
<img src="../images/2026-04-18/01-opening-narration.png" alt="..." />
```

## 4. 삽입할 블록

`<!-- 스크린샷 자리 -->` 및 빈 `<div id="log0418-screenshot-area"></div>` 전체를  
`docs/devlog/gh-pages_2026-04-18_screenshot_block.html` 내용으로 **교체**한다.
