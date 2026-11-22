using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Lumera.DebuggerKit.Editor
{
    // 디버거 킷의 UI 프리팹을 코드로 만든다(Tools/Lumera/DebuggerKit/Build Prefabs).
    // 실행할 때마다 프리팹을 처음부터 다시 만들어 덮어쓴다. 모양을 바꾸려면 프리팹을 손으로 고치지 말고 이 파일과 DebuggerKitTheme를 고친다.
    //
    // 픽셀 글꼴이 번지지 않게 크기와 위치는 모두 정수로 두고, 캔버스는 정수 배율(ConstantPixelSize)로만 키운다.
    // 글자는 프리팹에서 TMP 기본 글꼴로 두고, 실행할 때 DebuggerKitCanvas가 갈무리11 비트맵 글꼴로 바꾼다.
    // 사각형 테두리는 uGUI Outline 대신 4변 1px 선(AddBorder)으로 그려 모서리 번짐과 이중선을 없앤다.
    public static class DebuggerKitPrefabBuilder
    {
        private const string ROOT = "Packages/com.lumera.debuggerkit/Runtime/Unity";
        private const string FOLDER = ROOT + "/Prefabs";
        private const string CANVAS_PATH = FOLDER + "/DebuggerKitCanvas.prefab";
        private const string FONT_PATH = ROOT + "/Fonts/Galmuri11.ttf";
        private const string BITMAP_SHADER = "TextMeshPro/Mobile/Bitmap";
        private const int SORTING_ORDER = 30000;

        private const float HEIGHT = DebuggerKitTheme.WIDGET_HEIGHT;
        private const float LINE = DebuggerKitTheme.LINE_HEIGHT;
        // 콘솔이 화면 아래에서 차지하는 높이의 비율
        private const float CONSOLE_HEIGHT = 0.44f;
        private const float CONSOLE_HEADER = ConsoleView.HEADER_HEIGHT;
        private const float INPUT_HEIGHT = ConsoleView.INPUT_HEIGHT;
        private const float HINT_HEIGHT = ConsoleView.HINT_HEIGHT;
        private const float PAD = ConsoleView.PAD;
        private const float WIDGET_WIDTH = 216.0f;

        private static readonly Vector2 TOP_LEFT = new Vector2(0.0f, 1.0f);
        private static readonly Vector2 TOP_RIGHT = new Vector2(1.0f, 1.0f);
        private static readonly Vector2 MIDDLE_LEFT = new Vector2(0.0f, 0.5f);
        private static readonly Vector2 MIDDLE_RIGHT = new Vector2(1.0f, 0.5f);
        private static readonly Vector2 BOTTOM_LEFT = Vector2.zero;
        private static readonly Vector2 BOTTOM_RIGHT = new Vector2(1.0f, 0.0f);

        [MenuItem("Tools/Lumera/DebuggerKit/Build Prefabs")]
        public static void BuildFromMenu()
        {
            Build();
        }

        // 이미 만든 캔버스 프리팹. 아직 만들지 않았으면 null이다.
        public static DebuggerKitCanvas LoadCanvasOrNull()
        {
            return AssetDatabase.LoadAssetAtPath<DebuggerKitCanvas>(CANVAS_PATH);
        }

        // 모든 프리팹을 다시 만들고 캔버스 프리팹을 돌려준다. 씬 빌더가 DebuggerKitHost에 연결할 때 쓴다.
        public static DebuggerKitCanvas Build()
        {
            if (!AssetDatabase.IsValidFolder(FOLDER))
            {
                AssetDatabase.CreateFolder(ROOT, "Prefabs");
            }

            Font font = AssetDatabase.LoadAssetAtPath<Font>(FONT_PATH);
            Shader bitmapShader = Shader.Find(BITMAP_SHADER);
            if (font == null || bitmapShader == null)
            {
                throw new InvalidOperationException($"툴킷 글꼴({FONT_PATH}) 또는 TMP 비트맵 셰이더({BITMAP_SHADER})가 없습니다. TMP Essential Resources를 임포트했는지 확인하세요.");
            }

            Button segment = SavePrefab("SegmentButton", BuildSegmentButton).GetComponent<Button>();
            SuggestionRowView suggestionRow = SavePrefab("SuggestionRow", BuildSuggestionRow).GetComponent<SuggestionRowView>();
            ConsoleLineView consoleLine = SavePrefab("ConsoleLine", BuildConsoleLine).GetComponent<ConsoleLineView>();
            CategoryRowView categoryRow = SavePrefab("CategoryRow", BuildCategoryRow).GetComponent<CategoryRowView>();
            GroupHeaderView groupHeader = SavePrefab("GroupHeader", BuildGroupHeader).GetComponent<GroupHeaderView>();
            ArgRowView argRow = SavePrefab("ArgRow", root => BuildArgRow(root, segment)).GetComponent<ArgRowView>();
            CheatRowView cheatRow = SavePrefab("CheatRow", root => BuildCheatRow(root, segment, argRow)).GetComponent<CheatRowView>();
            DebuggerKitCanvas canvas = SavePrefab("DebuggerKitCanvas", root =>
            {
                ConsoleView console = BuildConsole(root, consoleLine, suggestionRow);
                CheatMenuView menu = BuildMenu(root, categoryRow, groupHeader, cheatRow, suggestionRow);
                ToastView toast = BuildToast(root);
                BuildCanvas(root, console, menu, toast, font, bitmapShader);
            }).GetComponent<DebuggerKitCanvas>();
            AssetDatabase.SaveAssets();
            return canvas;
        }

        private static void BuildCanvas(RectTransform root, ConsoleView console, CheatMenuView menu, ToastView toast, Font font, Shader bitmapShader)
        {
            Canvas canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SORTING_ORDER;
            canvas.pixelPerfect = true;
            // 배율은 DebuggerKitCanvas가 화면 높이에 맞춰 정수로 정한다.
            CanvasScaler scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1.0f;
            root.gameObject.AddComponent<GraphicRaycaster>();
            DebuggerKitCanvas view = root.gameObject.AddComponent<DebuggerKitCanvas>();
            using (SerializedObject serialized = new SerializedObject(view))
            {
                Wire(serialized, "_console", console);
                Wire(serialized, "_menu", menu);
                Wire(serialized, "_toast", toast);
                Wire(serialized, "_font", font);
                Wire(serialized, "_bitmapShader", bitmapShader);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // 화면 아래의 콘솔. 위에서부터 머리줄, 기록, 입력 줄, (입력 중일 때만) 힌트 바. 후보 목록은 입력 줄 바로 위에 뜬다.
        private static ConsoleView BuildConsole(RectTransform canvas, ConsoleLineView linePrefab, SuggestionRowView suggestionRow)
        {
            RectTransform root = Child(canvas, "Console");
            Stretch(root, 0.0f, 0.0f, 0.0f, 0.0f);
            ConsoleView view = root.gameObject.AddComponent<ConsoleView>();

            Image panel = AddImage(root, "Panel", DebuggerKitTheme.PANEL, true);
            panel.rectTransform.anchorMin = Vector2.zero;
            panel.rectTransform.anchorMax = new Vector2(1.0f, CONSOLE_HEIGHT);
            panel.rectTransform.offsetMin = Vector2.zero;
            panel.rectTransform.offsetMax = Vector2.zero;
            Image topAccent = AddImage(panel.rectTransform, "TopAccent", DebuggerKitTheme.BORDER_ACCENT, false);
            PlaceTopStretch(topAccent.rectTransform, 0.0f, 0.0f, 0.0f, 2.0f);

            // 머리줄: 악센트 막대, 제목, 오른쪽에 지우기·치트 메뉴·닫기
            Image header = AddImage(panel.rectTransform, "Header", DebuggerKitTheme.HEADER, false);
            PlaceTopStretch(header.rectTransform, 0.0f, 0.0f, 2.0f, CONSOLE_HEADER - 2.0f);
            Image headerBottom = AddImage(header.rectTransform, "BottomLine", DebuggerKitTheme.LINE, false);
            PlaceBottomStretch(headerBottom.rectTransform, 0.0f, 0.0f, 0.0f, 1.0f);
            Image titleAccent = AddImage(header.rectTransform, "TitleAccent", DebuggerKitTheme.ACCENT, false);
            Place(titleAccent.rectTransform, MIDDLE_LEFT, MIDDLE_LEFT, PAD, 0.0f, 3.0f, 12.0f);
            TextMeshProUGUI title = AddLabel(header.rectTransform, "Title", "개발자 콘솔", DebuggerKitTheme.TEXT, TextAlignmentOptions.MidlineLeft);
            Stretch(title.rectTransform, PAD + 8.0f, 0.0f, 220.0f, 0.0f);

            Button close = AddButton(header.rectTransform, "Close", "×", DebuggerKitTheme.TRANSPARENT, DebuggerKitTheme.BUTTON_TEXT, out _);
            Place((RectTransform)close.transform, MIDDLE_RIGHT, MIDDLE_RIGHT, -6.0f, 0.0f, 22.0f, HEIGHT);
            Button openMenu = AddButton(header.rectTransform, "OpenMenu", "치트 메뉴", DebuggerKitTheme.BUTTON, DebuggerKitTheme.BUTTON_TEXT, out _);
            Place((RectTransform)openMenu.transform, MIDDLE_RIGHT, MIDDLE_RIGHT, -32.0f, 0.0f, 70.0f, HEIGHT);
            AddBorder((RectTransform)openMenu.transform, DebuggerKitTheme.BORDER);
            Button clear = AddButton(header.rectTransform, "Clear", "지우기", DebuggerKitTheme.BUTTON, DebuggerKitTheme.MUTED, out _);
            Place((RectTransform)clear.transform, MIDDLE_RIGHT, MIDDLE_RIGHT, -108.0f, 0.0f, 52.0f, HEIGHT);
            AddBorder((RectTransform)clear.transform, DebuggerKitTheme.BORDER);

            ScrollRect lines = AddScroll(panel.rectTransform, "Lines", out RectTransform linesContent);
            lines.GetComponent<Image>().color = DebuggerKitTheme.SURFACE;
            Stretch((RectTransform)lines.transform, PAD, PAD + INPUT_HEIGHT + 6.0f, PAD, CONSOLE_HEADER + 6.0f);

            // 입력 줄: 프롬프트, 입력칸(위에 회색 완성), 실행 버튼
            Image inputRow = AddImage(panel.rectTransform, "InputRow", DebuggerKitTheme.FIELD, false);
            PlaceBottomStretch(inputRow.rectTransform, PAD, PAD, PAD, INPUT_HEIGHT);
            AddBorder(inputRow.rectTransform, DebuggerKitTheme.BORDER);
            TextMeshProUGUI prompt = AddLabel(inputRow.rectTransform, "Prompt", ">", DebuggerKitTheme.ACCENT, TextAlignmentOptions.Center);
            Place(prompt.rectTransform, MIDDLE_LEFT, MIDDLE_LEFT, 2.0f, 0.0f, 20.0f, INPUT_HEIGHT);
            KeyFilteredInputField input = AddInput(inputRow.rectTransform, "Input", "명령을 입력하세요 (help)", DebuggerKitTheme.TRANSPARENT, out TextMeshProUGUI inputText);
            Stretch((RectTransform)input.transform, 20.0f, 2.0f, 56.0f, 2.0f);
            // 회색 완성은 입력 글자와 같은 영역에 겹쳐 그린다. 글자가 길어 옆으로 밀릴 때 함께 밀리도록 글자의 자식으로 둔다.
            TextMeshProUGUI ghost = AddLabel(inputText.rectTransform, "Ghost", "", DebuggerKitTheme.PLACEHOLDER, TextAlignmentOptions.MidlineLeft);
            Stretch(ghost.rectTransform, 0.0f, 0.0f, 0.0f, 0.0f);
            ghost.margin = inputText.margin;
            ghost.richText = true;
            Button run = AddButton(inputRow.rectTransform, "Run", "실행", DebuggerKitTheme.RUN, DebuggerKitTheme.RUN_TEXT, out _);
            Place((RectTransform)run.transform, MIDDLE_RIGHT, MIDDLE_RIGHT, -4.0f, 0.0f, 48.0f, HEIGHT);

            // 힌트 바: 평소(빈 줄)에는 숨기고, 시그니처·오류·설명 힌트가 있을 때만 보인다.
            Image hintBar = AddImage(panel.rectTransform, "HintBar", DebuggerKitTheme.HINT_BG, false);
            PlaceBottomStretch(hintBar.rectTransform, 0.0f, 0.0f, 0.0f, HINT_HEIGHT);
            Image hintTopLine = AddImage(hintBar.rectTransform, "TopLine", DebuggerKitTheme.LINE, false);
            PlaceTopStretch(hintTopLine.rectTransform, 0.0f, 0.0f, 0.0f, 1.0f);
            TextMeshProUGUI hint = AddLabel(hintBar.rectTransform, "Hint", "", DebuggerKitTheme.MUTED, TextAlignmentOptions.MidlineLeft);
            Stretch(hint.rectTransform, PAD + 4.0f, 2.0f, PAD + 4.0f, 3.0f);
            hint.richText = true;
            hint.overflowMode = TextOverflowModes.Ellipsis;
            hint.margin = new Vector4(2.0f, 0.0f, 2.0f, 0.0f);
            hintBar.gameObject.SetActive(false);

            SuggestionListView suggestions = AddSuggestionList(root, "Suggestions", suggestionRow, 560.0f);
            RectTransform suggestionsRect = (RectTransform)suggestions.transform;
            suggestionsRect.anchorMin = BOTTOM_LEFT;
            suggestionsRect.anchorMax = BOTTOM_LEFT;
            suggestionsRect.pivot = BOTTOM_LEFT;
            suggestionsRect.anchoredPosition = new Vector2(PAD + 22.0f, PAD + INPUT_HEIGHT + 2.0f);

            using (SerializedObject serialized = new SerializedObject(view))
            {
                Wire(serialized, "_linesScroll", lines);
                Wire(serialized, "_linesContent", linesContent);
                Wire(serialized, "_linePrefab", linePrefab);
                Wire(serialized, "_inputRow", inputRow.rectTransform);
                Wire(serialized, "_input", input);
                Wire(serialized, "_ghost", ghost);
                Wire(serialized, "_hintBar", hintBar.rectTransform);
                Wire(serialized, "_hint", hint);
                Wire(serialized, "_suggestions", suggestions);
                Wire(serialized, "_run", run);
                Wire(serialized, "_clear", clear);
                Wire(serialized, "_close", close);
                Wire(serialized, "_openMenu", openMenu);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            return view;
        }

        // 화면 가운데의 치트 메뉴. 머리줄(제목, 검색, 버튼), 왼쪽 분류, 오른쪽 치트 목록, 아래 설명 줄.
        private static CheatMenuView BuildMenu(RectTransform canvas, CategoryRowView categoryRow, GroupHeaderView groupHeader, CheatRowView cheatRow, SuggestionRowView suggestionRow)
        {
            const float HEADER = 34.0f;
            const float FOOTER = 24.0f;
            const float SIDEBAR = 160.0f;

            RectTransform root = Child(canvas, "Menu");
            Stretch(root, 0.0f, 0.0f, 0.0f, 0.0f);
            CheatMenuView view = root.gameObject.AddComponent<CheatMenuView>();
            // 메뉴 밖을 눌러도 게임 UI가 눌리지 않게 화면 전체를 가린다.
            Image dim = AddImage(root, "Dim", DebuggerKitTheme.DIM, true);
            Stretch(dim.rectTransform, 0.0f, 0.0f, 0.0f, 0.0f);

            Image panel = AddImage(root, "Panel", DebuggerKitTheme.PANEL, true);
            panel.rectTransform.anchorMin = new Vector2(0.10f, 0.07f);
            panel.rectTransform.anchorMax = new Vector2(0.90f, 0.93f);
            panel.rectTransform.offsetMin = Vector2.zero;
            panel.rectTransform.offsetMax = Vector2.zero;
            AddBorder(panel.rectTransform, DebuggerKitTheme.BORDER);
            Image topAccent = AddImage(panel.rectTransform, "TopAccent", DebuggerKitTheme.BORDER_ACCENT, false);
            PlaceTopStretch(topAccent.rectTransform, 1.0f, 1.0f, 0.0f, 2.0f);

            Image header = AddImage(panel.rectTransform, "Header", DebuggerKitTheme.HEADER, false);
            PlaceTopStretch(header.rectTransform, 1.0f, 1.0f, 2.0f, HEADER - 2.0f);
            Image headerLine = AddImage(header.rectTransform, "BottomLine", DebuggerKitTheme.LINE, false);
            PlaceBottomStretch(headerLine.rectTransform, 0.0f, 0.0f, 0.0f, 1.0f);
            Image titleAccent = AddImage(header.rectTransform, "TitleAccent", DebuggerKitTheme.ACCENT, false);
            Place(titleAccent.rectTransform, MIDDLE_LEFT, MIDDLE_LEFT, PAD, 0.0f, 3.0f, 12.0f);
            TextMeshProUGUI title = AddLabel(header.rectTransform, "Title", "치트 메뉴", DebuggerKitTheme.TEXT, TextAlignmentOptions.MidlineLeft);
            Place(title.rectTransform, MIDDLE_LEFT, MIDDLE_LEFT, PAD + 8.0f, 0.0f, SIDEBAR - PAD - 12.0f, HEADER);
            KeyFilteredInputField search = AddInput(header.rectTransform, "Search", "검색 (이름, id, 분류)", DebuggerKitTheme.FIELD, out _);
            RectTransform searchRect = (RectTransform)search.transform;
            searchRect.anchorMin = new Vector2(0.0f, 0.5f);
            searchRect.anchorMax = new Vector2(1.0f, 0.5f);
            searchRect.pivot = MIDDLE_LEFT;
            searchRect.offsetMin = new Vector2(SIDEBAR, -11.0f);
            searchRect.offsetMax = new Vector2(-154.0f, 11.0f);
            AddBorder(searchRect, DebuggerKitTheme.BORDER);
            Button close = AddButton(header.rectTransform, "Close", "×", DebuggerKitTheme.TRANSPARENT, DebuggerKitTheme.BUTTON_TEXT, out _);
            Place((RectTransform)close.transform, MIDDLE_RIGHT, MIDDLE_RIGHT, -6.0f, 0.0f, 22.0f, HEIGHT);
            Button openConsole = AddButton(header.rectTransform, "OpenConsole", "콘솔", DebuggerKitTheme.BUTTON, DebuggerKitTheme.BUTTON_TEXT, out _);
            Place((RectTransform)openConsole.transform, MIDDLE_RIGHT, MIDDLE_RIGHT, -32.0f, 0.0f, 50.0f, HEIGHT);
            AddBorder((RectTransform)openConsole.transform, DebuggerKitTheme.BORDER);
            Button collapseAll = AddButton(header.rectTransform, "CollapseAll", "접기", DebuggerKitTheme.BUTTON, DebuggerKitTheme.BUTTON_TEXT, out _);
            Place((RectTransform)collapseAll.transform, MIDDLE_RIGHT, MIDDLE_RIGHT, -88.0f, 0.0f, 48.0f, HEIGHT);
            AddBorder((RectTransform)collapseAll.transform, DebuggerKitTheme.BORDER);

            Image sidebar = AddImage(panel.rectTransform, "Sidebar", DebuggerKitTheme.SIDEBAR, false);
            sidebar.rectTransform.anchorMin = Vector2.zero;
            sidebar.rectTransform.anchorMax = new Vector2(0.0f, 1.0f);
            sidebar.rectTransform.pivot = TOP_LEFT;
            sidebar.rectTransform.offsetMin = new Vector2(1.0f, FOOTER);
            sidebar.rectTransform.offsetMax = new Vector2(SIDEBAR, -HEADER);
            Image sidebarLine = AddImage(sidebar.rectTransform, "Line", DebuggerKitTheme.LINE, false);
            sidebarLine.rectTransform.anchorMin = BOTTOM_RIGHT;
            sidebarLine.rectTransform.anchorMax = Vector2.one;
            sidebarLine.rectTransform.pivot = MIDDLE_RIGHT;
            sidebarLine.rectTransform.offsetMin = new Vector2(-1.0f, 0.0f);
            sidebarLine.rectTransform.offsetMax = Vector2.zero;
            ScrollRect categories = AddScroll(sidebar.rectTransform, "Categories", out RectTransform categoryContent);
            Stretch((RectTransform)categories.transform, 0.0f, 4.0f, 1.0f, 4.0f);

            ScrollRect list = AddScroll(panel.rectTransform, "List", out RectTransform listContent);
            Stretch((RectTransform)list.transform, SIDEBAR, FOOTER, 1.0f, HEADER);
            TextMeshProUGUI empty = AddLabel((RectTransform)list.transform, "Empty", "", DebuggerKitTheme.MUTED, TextAlignmentOptions.Center);
            Stretch(empty.rectTransform, 0.0f, 0.0f, 0.0f, 0.0f);

            Image footerBar = AddImage(panel.rectTransform, "FooterBar", DebuggerKitTheme.HEADER, false);
            PlaceBottomStretch(footerBar.rectTransform, 1.0f, 1.0f, 1.0f, FOOTER - 1.0f);
            Image footerTopLine = AddImage(footerBar.rectTransform, "TopLine", DebuggerKitTheme.LINE, false);
            PlaceTopStretch(footerTopLine.rectTransform, 0.0f, 0.0f, 0.0f, 1.0f);
            TextMeshProUGUI footer = AddLabel(footerBar.rectTransform, "Footer", "", DebuggerKitTheme.MUTED, TextAlignmentOptions.MidlineLeft);
            Stretch(footer.rectTransform, PAD, 0.0f, PAD, 0.0f);
            footer.overflowMode = TextOverflowModes.Ellipsis;
            footer.margin = new Vector4(2.0f, 0.0f, 2.0f, 0.0f);

            // 인자 입력칸 아래에 띄우는 후보 목록. 위치는 실행 중에 입력칸에 맞춘다.
            SuggestionListView popup = AddSuggestionList(panel.rectTransform, "Popup", suggestionRow, 320.0f);

            using (SerializedObject serialized = new SerializedObject(view))
            {
                Wire(serialized, "_search", search);
                Wire(serialized, "_close", close);
                Wire(serialized, "_openConsole", openConsole);
                Wire(serialized, "_collapseAll", collapseAll);
                Wire(serialized, "_categoryContent", categoryContent);
                Wire(serialized, "_categoryPrefab", categoryRow);
                Wire(serialized, "_listScroll", list);
                Wire(serialized, "_listContent", listContent);
                Wire(serialized, "_rowPrefab", cheatRow);
                Wire(serialized, "_headerPrefab", groupHeader);
                Wire(serialized, "_empty", empty);
                Wire(serialized, "_footer", footer);
                Wire(serialized, "_popup", popup);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            return view;
        }

        private static ToastView BuildToast(RectTransform canvas)
        {
            RectTransform root = Child(canvas, "Toast");
            Stretch(root, 0.0f, 0.0f, 0.0f, 0.0f);
            ToastView view = root.gameObject.AddComponent<ToastView>();
            Image background = AddImage(root, "Background", DebuggerKitTheme.HEADER, false);
            Place(background.rectTransform, new Vector2(0.5f, 0.0f), new Vector2(0.5f, 0.0f), 0.0f, 28.0f, 200.0f, 28.0f);
            AddBorder(background.rectTransform, DebuggerKitTheme.BORDER);
            // 메시지 길이에 맞춰 배경이 늘어난다.
            HorizontalLayoutGroup layout = background.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 6, 6);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = background.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            TextMeshProUGUI text = AddLabel(background.rectTransform, "Text", "", DebuggerKitTheme.TEXT, TextAlignmentOptions.Center);

            using (SerializedObject serialized = new SerializedObject(view))
            {
                Wire(serialized, "_background", background);
                Wire(serialized, "_text", text);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            return view;
        }

        private static void BuildSuggestionRow(RectTransform root)
        {
            Place(root, TOP_LEFT, TOP_LEFT, 0.0f, 0.0f, 320.0f, DebuggerKitTheme.ROW_HEIGHT);
            Image background = root.gameObject.AddComponent<Image>();
            background.color = DebuggerKitTheme.TRANSPARENT;
            SuggestionRowView view = root.gameObject.AddComponent<SuggestionRowView>();
            Image marker = AddImage(root, "Marker", DebuggerKitTheme.ACCENT, false);
            marker.rectTransform.anchorMin = Vector2.zero;
            marker.rectTransform.anchorMax = new Vector2(0.0f, 1.0f);
            marker.rectTransform.pivot = MIDDLE_LEFT;
            marker.rectTransform.offsetMin = Vector2.zero;
            marker.rectTransform.offsetMax = new Vector2(2.0f, 0.0f);
            marker.enabled = false;
            TextMeshProUGUI label = AddLabel(root, "Label", "", DebuggerKitTheme.TEXT, TextAlignmentOptions.MidlineLeft);
            Stretch(label.rectTransform, 8.0f, 0.0f, 116.0f, 0.0f);
            label.richText = true;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.margin = new Vector4(2.0f, 0.0f, 2.0f, 0.0f);
            TextMeshProUGUI note = AddLabel(root, "Note", "", DebuggerKitTheme.MUTED, TextAlignmentOptions.MidlineRight);
            Place(note.rectTransform, MIDDLE_RIGHT, MIDDLE_RIGHT, -8.0f, 0.0f, 104.0f, DebuggerKitTheme.ROW_HEIGHT);
            note.overflowMode = TextOverflowModes.Ellipsis;

            using (SerializedObject serialized = new SerializedObject(view))
            {
                Wire(serialized, "_background", background);
                Wire(serialized, "_marker", marker);
                Wire(serialized, "_label", label);
                Wire(serialized, "_note", note);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void BuildConsoleLine(RectTransform root)
        {
            root.anchorMin = TOP_LEFT;
            root.anchorMax = TOP_RIGHT;
            root.pivot = TOP_LEFT;
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = new Vector2(0.0f, LINE);
            Image background = root.gameObject.AddComponent<Image>();
            background.color = DebuggerKitTheme.TRANSPARENT;
            background.raycastTarget = false;
            TextMeshProUGUI text = AddLabel(root, "Text", "", DebuggerKitTheme.TEXT, TextAlignmentOptions.MidlineLeft);
            Stretch(text.rectTransform, 0.0f, 0.0f, 0.0f, 0.0f);
            text.overflowMode = TextOverflowModes.Ellipsis;
            // RectMask2D 경계에서 첫 글자의 왼쪽 획이 잘리지 않도록 안전 여백을 둔다.
            text.margin = new Vector4(6.0f, 0.0f, 6.0f, 0.0f);
            ConsoleLineView view = root.gameObject.AddComponent<ConsoleLineView>();
            using (SerializedObject serialized = new SerializedObject(view))
            {
                Wire(serialized, "_background", background);
                Wire(serialized, "_text", text);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void BuildCategoryRow(RectTransform root)
        {
            root.anchorMin = TOP_LEFT;
            root.anchorMax = TOP_RIGHT;
            root.pivot = TOP_LEFT;
            root.sizeDelta = new Vector2(0.0f, DebuggerKitTheme.ROW_HEIGHT);
            Image background = root.gameObject.AddComponent<Image>();
            background.color = DebuggerKitTheme.TRANSPARENT;
            Button button = AddButtonBehaviour(root.gameObject, background);
            CategoryRowView view = root.gameObject.AddComponent<CategoryRowView>();
            Image marker = AddImage(root, "Marker", DebuggerKitTheme.ACCENT, false);
            marker.rectTransform.anchorMin = Vector2.zero;
            marker.rectTransform.anchorMax = new Vector2(0.0f, 1.0f);
            marker.rectTransform.pivot = MIDDLE_LEFT;
            marker.rectTransform.offsetMin = Vector2.zero;
            marker.rectTransform.offsetMax = new Vector2(3.0f, 0.0f);
            TextMeshProUGUI label = AddLabel(root, "Label", "", DebuggerKitTheme.TEXT, TextAlignmentOptions.MidlineLeft);
            Stretch(label.rectTransform, 0.0f, 0.0f, 34.0f, 0.0f);
            label.overflowMode = TextOverflowModes.Ellipsis;
            TextMeshProUGUI count = AddLabel(root, "Count", "", DebuggerKitTheme.MUTED, TextAlignmentOptions.MidlineRight);
            Place(count.rectTransform, MIDDLE_RIGHT, MIDDLE_RIGHT, -10.0f, 0.0f, 26.0f, DebuggerKitTheme.ROW_HEIGHT);

            using (SerializedObject serialized = new SerializedObject(view))
            {
                Wire(serialized, "_button", button);
                Wire(serialized, "_marker", marker);
                Wire(serialized, "_label", label);
                Wire(serialized, "_count", count);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void BuildGroupHeader(RectTransform root)
        {
            root.anchorMin = TOP_LEFT;
            root.anchorMax = TOP_RIGHT;
            root.pivot = TOP_LEFT;
            root.sizeDelta = new Vector2(0.0f, DebuggerKitTheme.GROUP_HEIGHT);
            Image background = root.gameObject.AddComponent<Image>();
            background.color = DebuggerKitTheme.GROUP;
            Button button = AddButtonBehaviour(root.gameObject, background);
            GroupHeaderView view = root.gameObject.AddComponent<GroupHeaderView>();
            Image bottomLine = AddImage(root, "BottomLine", DebuggerKitTheme.LINE, false);
            PlaceBottomStretch(bottomLine.rectTransform, 0.0f, 0.0f, 0.0f, 1.0f);
            TextMeshProUGUI label = AddLabel(root, "Label", "", DebuggerKitTheme.TEXT, TextAlignmentOptions.MidlineLeft);
            Stretch(label.rectTransform, PAD, 0.0f, 44.0f, 0.0f);
            label.margin = new Vector4(2.0f, 0.0f, 2.0f, 0.0f);
            TextMeshProUGUI count = AddLabel(root, "Count", "", DebuggerKitTheme.MUTED, TextAlignmentOptions.MidlineRight);
            Place(count.rectTransform, MIDDLE_RIGHT, MIDDLE_RIGHT, -12.0f, 0.0f, 32.0f, DebuggerKitTheme.GROUP_HEIGHT);

            using (SerializedObject serialized = new SerializedObject(view))
            {
                Wire(serialized, "_button", button);
                Wire(serialized, "_label", label);
                Wire(serialized, "_count", count);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void BuildSegmentButton(RectTransform root)
        {
            Place(root, MIDDLE_LEFT, MIDDLE_LEFT, 0.0f, 0.0f, 44.0f, HEIGHT);
            Image background = root.gameObject.AddComponent<Image>();
            background.color = DebuggerKitTheme.BUTTON;
            AddButtonBehaviour(root.gameObject, background);
            AddBorder(root, DebuggerKitTheme.BORDER);
            TextMeshProUGUI label = AddLabel(root, "Label", "", DebuggerKitTheme.MUTED, TextAlignmentOptions.Center);
            Stretch(label.rectTransform, 2.0f, 0.0f, 2.0f, 0.0f);
        }

        private static void BuildArgRow(RectTransform root, Button segmentPrefab)
        {
            root.anchorMin = TOP_LEFT;
            root.anchorMax = TOP_RIGHT;
            root.pivot = TOP_LEFT;
            root.sizeDelta = new Vector2(0.0f, CheatRowView.ARG_ROW_HEIGHT);
            ArgRowView view = root.gameObject.AddComponent<ArgRowView>();
            TextMeshProUGUI label = AddLabel(root, "Label", "", DebuggerKitTheme.MUTED, TextAlignmentOptions.MidlineLeft);
            Place(label.rectTransform, MIDDLE_LEFT, MIDDLE_LEFT, 0.0f, 0.0f, 154.0f, CheatRowView.ARG_ROW_HEIGHT);
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.margin = new Vector4(2.0f, 0.0f, 2.0f, 0.0f);
            ValueWidget widget = AddValueWidget(root, "Widget", segmentPrefab, false);
            Place((RectTransform)widget.transform, MIDDLE_LEFT, MIDDLE_LEFT, 160.0f, 0.0f, WIDGET_WIDTH, HEIGHT);

            using (SerializedObject serialized = new SerializedObject(view))
            {
                Wire(serialized, "_label", label);
                Wire(serialized, "_widget", widget);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void BuildCheatRow(RectTransform root, Button segmentPrefab, ArgRowView argPrefab)
        {
            const float ROW = DebuggerKitTheme.MENU_ROW_HEIGHT;
            const float INDENT = 28.0f;

            root.anchorMin = TOP_LEFT;
            root.anchorMax = TOP_RIGHT;
            root.pivot = TOP_LEFT;
            root.sizeDelta = new Vector2(0.0f, ROW);
            CheatRowView view = root.gameObject.AddComponent<CheatRowView>();
            // 행 어디에 마우스를 올려도 설명이 보이도록 배경이 포인터를 받는다.
            Image background = AddImage(root, "Background", DebuggerKitTheme.TRANSPARENT, true);
            Stretch(background.rectTransform, 0.0f, 0.0f, 0.0f, 0.0f);
            Image separator = AddImage(root, "Separator", DebuggerKitTheme.LINE, false);
            PlaceBottomStretch(separator.rectTransform, 0.0f, 0.0f, 0.0f, 1.0f);

            RectTransform main = Child(root, "Main");
            PlaceTopStretch(main, 0.0f, 0.0f, 0.0f, ROW);
            Button favorite = AddButton(main, "Favorite", "☆", DebuggerKitTheme.TRANSPARENT, DebuggerKitTheme.MUTED, out TextMeshProUGUI favoriteLabel);
            Place((RectTransform)favorite.transform, MIDDLE_LEFT, MIDDLE_LEFT, 6.0f, 0.0f, 18.0f, HEIGHT);
            TextMeshProUGUI title = AddLabel(main, "Title", "", DebuggerKitTheme.TEXT, TextAlignmentOptions.MidlineLeft);
            Stretch(title.rectTransform, INDENT, 0.0f, WIDGET_WIDTH + 66.0f, 0.0f);
            title.richText = true;
            title.overflowMode = TextOverflowModes.Ellipsis;
            title.margin = new Vector4(2.0f, 0.0f, 2.0f, 0.0f);
            TextMeshProUGUI status = AddLabel(main, "Status", "", DebuggerKitTheme.SUCCESS, TextAlignmentOptions.MidlineRight);
            Place(status.rectTransform, MIDDLE_RIGHT, MIDDLE_RIGHT, -(WIDGET_WIDTH + 16.0f), 0.0f, 44.0f, ROW);
            Button run = AddButton(main, "Run", "실행", DebuggerKitTheme.RUN, DebuggerKitTheme.RUN_TEXT, out TextMeshProUGUI runLabel);
            Place((RectTransform)run.transform, MIDDLE_RIGHT, MIDDLE_RIGHT, -10.0f, 0.0f, 48.0f, HEIGHT);
            ValueWidget valueWidget = AddValueWidget(main, "Value", segmentPrefab, true);
            Place((RectTransform)valueWidget.transform, MIDDLE_RIGHT, MIDDLE_RIGHT, -10.0f, 0.0f, WIDGET_WIDTH, HEIGHT);

            RectTransform editor = Child(root, "Editor");
            PlaceTopStretch(editor, INDENT, 10.0f, ROW, CheatRowView.ARG_ROW_HEIGHT);
            Button execute = AddButton(editor, "Execute", "실행", DebuggerKitTheme.RUN, DebuggerKitTheme.RUN_TEXT, out _);
            Place((RectTransform)execute.transform, TOP_LEFT, TOP_LEFT, 160.0f, 0.0f, 48.0f, HEIGHT);
            editor.gameObject.SetActive(false);

            using (SerializedObject serialized = new SerializedObject(view))
            {
                Wire(serialized, "_background", background);
                Wire(serialized, "_favorite", favorite);
                Wire(serialized, "_favoriteLabel", favoriteLabel);
                Wire(serialized, "_title", title);
                Wire(serialized, "_status", status);
                Wire(serialized, "_run", run);
                Wire(serialized, "_runLabel", runLabel);
                Wire(serialized, "_valueWidget", valueWidget);
                Wire(serialized, "_editor", editor);
                Wire(serialized, "_argPrefab", argPrefab);
                Wire(serialized, "_execute", execute);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // 타입마다 쓰는 컨트롤을 모두 넣어 두고, 실행할 때 맞는 것만 켠다.
        // bRightAligned: 행의 오른쪽 끝에 붙는 위젯(값 치트)은 내용도 오른쪽으로 붙인다.
        private static ValueWidget AddValueWidget(RectTransform parent, string name, Button segmentPrefab, bool bRightAligned)
        {
            const float STEP_WIDTH = 20.0f;
            const float FIELD_WIDTH = 128.0f;
            const float GROUP_WIDTH = FIELD_WIDTH + ((STEP_WIDTH + 2.0f) * 2.0f);
            const float SWITCH_WIDTH = 30.0f;
            const float SWITCH_HEIGHT = 16.0f;
            const float KNOB = 12.0f;

            Vector2 side = bRightAligned ? MIDDLE_RIGHT : MIDDLE_LEFT;
            RectTransform root = Child(parent, name);
            ValueWidget widget = root.gameObject.AddComponent<ValueWidget>();

            // 읽기 전용 프로퍼티 값 배지
            Image readOnlyBadge = AddImage(root, "ReadOnlyBadge", DebuggerKitTheme.BADGE, false);
            Place(readOnlyBadge.rectTransform, side, side, 0.0f, 0.0f, 108.0f, HEIGHT);
            AddBorder(readOnlyBadge.rectTransform, DebuggerKitTheme.LINE);
            TextMeshProUGUI readOnly = AddLabel(readOnlyBadge.rectTransform, "ReadOnly", "", DebuggerKitTheme.ACCENT, bRightAligned ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft);
            Stretch(readOnly.rectTransform, 8.0f, 0.0f, 8.0f, 0.0f);
            readOnly.overflowMode = TextOverflowModes.Ellipsis;

            Image track = AddImage(root, "Switch", DebuggerKitTheme.SWITCH_OFF, true);
            Place(track.rectTransform, side, side, 0.0f, 0.0f, SWITCH_WIDTH, SWITCH_HEIGHT);
            AddBorder(track.rectTransform, DebuggerKitTheme.BORDER);
            Button switchButton = AddButtonBehaviour(track.gameObject, track);
            Image knob = AddImage(track.rectTransform, "Knob", DebuggerKitTheme.KNOB, false);
            Place(knob.rectTransform, MIDDLE_LEFT, MIDDLE_LEFT, 2.0f, 0.0f, KNOB, KNOB);

            RectTransform segments = Child(root, "Segments");
            Place(segments, side, side, 0.0f, 0.0f, 0.0f, HEIGHT);

            RectTransform fieldGroup = Child(root, "FieldGroup");
            Place(fieldGroup, side, side, 0.0f, 0.0f, GROUP_WIDTH, HEIGHT);
            KeyFilteredInputField field = AddInput(fieldGroup, "Field", "", DebuggerKitTheme.FIELD, out _);
            Place((RectTransform)field.transform, MIDDLE_LEFT, MIDDLE_LEFT, 0.0f, 0.0f, FIELD_WIDTH, HEIGHT);
            AddBorder((RectTransform)field.transform, DebuggerKitTheme.BORDER);
            Button minus = AddButton(fieldGroup, "Minus", "-", DebuggerKitTheme.BUTTON, DebuggerKitTheme.TEXT, out _);
            Place((RectTransform)minus.transform, MIDDLE_LEFT, MIDDLE_LEFT, FIELD_WIDTH + 2.0f, 0.0f, STEP_WIDTH, HEIGHT);
            AddBorder((RectTransform)minus.transform, DebuggerKitTheme.BORDER);
            Button plus = AddButton(fieldGroup, "Plus", "+", DebuggerKitTheme.BUTTON, DebuggerKitTheme.TEXT, out _);
            Place((RectTransform)plus.transform, MIDDLE_LEFT, MIDDLE_LEFT, FIELD_WIDTH + STEP_WIDTH + 4.0f, 0.0f, STEP_WIDTH, HEIGHT);
            AddBorder((RectTransform)plus.transform, DebuggerKitTheme.BORDER);

            using (SerializedObject serialized = new SerializedObject(widget))
            {
                Wire(serialized, "_switch", switchButton);
                Wire(serialized, "_knob", knob.rectTransform);
                Wire(serialized, "_segments", segments);
                Wire(serialized, "_segmentPrefab", segmentPrefab);
                Wire(serialized, "_fieldGroup", fieldGroup);
                Wire(serialized, "_field", field);
                Wire(serialized, "_minus", minus);
                Wire(serialized, "_plus", plus);
                Wire(serialized, "_readOnlyBadge", readOnlyBadge.gameObject);
                Wire(serialized, "_readOnly", readOnly);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            return widget;
        }

        // 후보 목록 패널. 기본은 왼쪽 위 모서리를 기준으로 아래로 늘어난다(콘솔은 기준을 왼쪽 아래로 바꿔 위로 늘린다).
        // 높이는 실행 중에 후보 수에 맞춘다.
        private static SuggestionListView AddSuggestionList(RectTransform parent, string name, SuggestionRowView rowPrefab, float width)
        {
            ScrollRect scroll = AddScroll(parent, name, out RectTransform content);
            RectTransform root = (RectTransform)scroll.transform;
            Place(root, BOTTOM_LEFT, TOP_LEFT, 0.0f, 0.0f, width, DebuggerKitTheme.ROW_HEIGHT);
            Image background = root.GetComponent<Image>();
            background.color = DebuggerKitTheme.POPUP;
            AddBorder(root, DebuggerKitTheme.BORDER);
            SuggestionListView view = root.gameObject.AddComponent<SuggestionListView>();
            using (SerializedObject serialized = new SerializedObject(view))
            {
                Wire(serialized, "_panel", root);
                Wire(serialized, "_scroll", scroll);
                Wire(serialized, "_content", content);
                Wire(serialized, "_rowPrefab", rowPrefab);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            return view;
        }

        // 세로로만 움직이는 스크롤 영역. 자기 자신이 보이는 범위(viewport)고, 내용은 위쪽에 붙어 아래로 늘어난다.
        private static ScrollRect AddScroll(RectTransform parent, string name, out RectTransform content)
        {
            // 휠과 드래그를 받으려면 그래픽이 있어야 한다. 보이지 않는 이미지를 둔다.
            Image background = AddImage(parent, name, DebuggerKitTheme.TRANSPARENT, true);
            background.gameObject.AddComponent<RectMask2D>();
            content = Child(background.rectTransform, "Content");
            content.anchorMin = TOP_LEFT;
            content.anchorMax = TOP_RIGHT;
            content.pivot = TOP_LEFT;
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            ScrollRect scroll = background.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = background.rectTransform;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = false;
            scroll.scrollSensitivity = LINE * 2.0f;
            return scroll;
        }

        private static KeyFilteredInputField AddInput(RectTransform parent, string name, string placeholderText, Color backgroundColor, out TextMeshProUGUI text)
        {
            Image background = AddImage(parent, name, backgroundColor, true);
            RectTransform area = Child(background.rectTransform, "TextArea");
            Stretch(area, 6.0f, 1.0f, 6.0f, 1.0f);
            area.gameObject.AddComponent<RectMask2D>();
            TextMeshProUGUI placeholder = AddLabel(area, "Placeholder", placeholderText, DebuggerKitTheme.PLACEHOLDER, TextAlignmentOptions.MidlineLeft);
            Stretch(placeholder.rectTransform, 0.0f, 0.0f, 0.0f, 0.0f);
            placeholder.overflowMode = TextOverflowModes.Ellipsis;
            placeholder.margin = new Vector4(2.0f, 0.0f, 2.0f, 0.0f);
            text = AddLabel(area, "Text", "", DebuggerKitTheme.TEXT, TextAlignmentOptions.MidlineLeft);
            Stretch(text.rectTransform, 0.0f, 0.0f, 0.0f, 0.0f);
            text.margin = new Vector4(2.0f, 0.0f, 2.0f, 0.0f);

            KeyFilteredInputField input = background.gameObject.AddComponent<KeyFilteredInputField>();
            input.targetGraphic = background;
            input.transition = Selectable.Transition.None;
            input.navigation = new Navigation { mode = Navigation.Mode.None };
            input.textViewport = area;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.pointSize = DebuggerKitTheme.FONT_SIZE;
            input.richText = false;
            input.onFocusSelectAll = false;
            input.resetOnDeActivation = false;
            input.restoreOriginalTextOnEscape = false;
            input.caretColor = DebuggerKitTheme.ACCENT;
            input.customCaretColor = true;
            input.caretWidth = 1;
            input.caretBlinkRate = 1.0f;
            input.selectionColor = new Color32(64, 128, 230, 120);
            return input;
        }

        private static Button AddButton(RectTransform parent, string name, string text, Color background, Color textColor, out TextMeshProUGUI label)
        {
            Image image = AddImage(parent, name, background, true);
            Button button = AddButtonBehaviour(image.gameObject, image);
            label = AddLabel(image.rectTransform, "Label", text, textColor, TextAlignmentOptions.Center);
            Stretch(label.rectTransform, 2.0f, 0.0f, 2.0f, 0.0f);
            return button;
        }

        // 방향키 이동(Navigation)을 끈다. 켜 두면 입력칸에서 ↑/↓를 누를 때 선택이 다른 버튼으로 넘어간다.
        // 눌림은 색을 살짝 밝게 해서 보인다(배경색을 그대로 쓰는 버튼이 많아서 색을 곱하는 방식을 쓴다).
        private static Button AddButtonBehaviour(GameObject target, Image background)
        {
            Button button = target.AddComponent<Button>();
            button.targetGraphic = background;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.22f, 1.22f, 1.22f, 1.0f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1.0f);
            colors.selectedColor = Color.white;
            colors.colorMultiplier = 1.0f;
            colors.fadeDuration = 0.0f;
            button.colors = colors;
            return button;
        }

        // uGUI Outline 대신 4변 1px 이미지로 테두리를 그린다. 모서리 겹침과 밝기 불균일이 없다.
        private static void AddBorder(RectTransform target, Color color)
        {
            Image top = AddImage(target, "BorderTop", color, false);
            PlaceTopStretch(top.rectTransform, 0.0f, 0.0f, 0.0f, 1.0f);

            Image bottom = AddImage(target, "BorderBottom", color, false);
            PlaceBottomStretch(bottom.rectTransform, 0.0f, 0.0f, 0.0f, 1.0f);

            Image left = AddImage(target, "BorderLeft", color, false);
            left.rectTransform.anchorMin = Vector2.zero;
            left.rectTransform.anchorMax = new Vector2(0.0f, 1.0f);
            left.rectTransform.pivot = MIDDLE_LEFT;
            left.rectTransform.offsetMin = new Vector2(0.0f, 1.0f);
            left.rectTransform.offsetMax = new Vector2(1.0f, -1.0f);

            Image right = AddImage(target, "BorderRight", color, false);
            right.rectTransform.anchorMin = BOTTOM_RIGHT;
            right.rectTransform.anchorMax = Vector2.one;
            right.rectTransform.pivot = MIDDLE_RIGHT;
            right.rectTransform.offsetMin = new Vector2(-1.0f, 1.0f);
            right.rectTransform.offsetMax = new Vector2(0.0f, -1.0f);
        }

        private static Image AddImage(RectTransform parent, string name, Color color, bool bRaycast)
        {
            Image image = Child(parent, name).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = bRaycast;
            return image;
        }

        private static TextMeshProUGUI AddLabel(RectTransform parent, string name, string text, Color color, TextAlignmentOptions alignment)
        {
            TextMeshProUGUI label = Child(parent, name).gameObject.AddComponent<TextMeshProUGUI>();
            SetLabel(label, text, color, alignment);
            return label;
        }

        private static void SetLabel(TextMeshProUGUI label, string text, Color color, TextAlignmentOptions alignment)
        {
            label.text = text;
            label.fontSize = DebuggerKitTheme.FONT_SIZE;
            label.enableAutoSizing = false;
            label.color = color;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.extraPadding = false;
            label.richText = false;
            label.raycastTarget = false;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, float x, float y, float width, float height)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        // 부모를 채우고 네 변에서 안쪽으로 띄운다.
        private static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        // 부모의 위쪽에 붙여 가로로 채운다. top은 위에서 내려온 거리다.
        private static void PlaceTopStretch(RectTransform rect, float left, float right, float top, float height)
        {
            rect.anchorMin = TOP_LEFT;
            rect.anchorMax = TOP_RIGHT;
            rect.pivot = TOP_LEFT;
            rect.offsetMin = new Vector2(left, -(top + height));
            rect.offsetMax = new Vector2(-right, -top);
        }

        // 부모의 아래쪽에 붙여 가로로 채운다. bottom은 아래에서 올라온 거리다.
        private static void PlaceBottomStretch(RectTransform rect, float left, float right, float bottom, float height)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = BOTTOM_RIGHT;
            rect.pivot = Vector2.zero;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, bottom + height);
        }

        private static RectTransform Child(RectTransform parent, string name)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return (RectTransform)child.transform;
        }

        private static GameObject SavePrefab(string name, Action<RectTransform> configure)
        {
            GameObject temporary = new GameObject(name, typeof(RectTransform));
            try
            {
                configure((RectTransform)temporary.transform);
                string path = $"{FOLDER}/{name}.prefab";
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temporary, path);
                if (prefab == null)
                {
                    throw new InvalidOperationException($"UI 프리팹을 저장할 수 없습니다. {path}");
                }

                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(temporary);
            }
        }

        private static void Wire(SerializedObject serialized, string field, UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                throw new InvalidOperationException($"{serialized.targetObject.GetType().Name}에 {field} 필드가 없습니다.");
            }

            property.objectReferenceValue = value;
        }
    }
}
