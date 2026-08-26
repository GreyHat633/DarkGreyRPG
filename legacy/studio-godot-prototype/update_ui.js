const fs = require('fs');
const path = require('path');

const baseDir = "E:/Java/MinecraftMod/DarkGrey_RPG/studio/scripts";
const mainGdPath = path.join(baseDir, "Main.gd");
let content = fs.readFileSync(mainGdPath, 'utf8');

// 1. Replace _build_ui
content = content.replace(
    /func _build_ui\(\) -> void:[\s\S]*?var upper := HSplitContainer\.new\(\)/,
`func _build_ui() -> void:
	var root := PanelContainer.new()
	self.theme = ThemeManager.get_theme()
	root.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(root)

	var main_vbox := VBoxContainer.new()
	main_vbox.add_theme_constant_override("separation", 0)
	root.add_child(main_vbox)

	main_vbox.add_child(_build_menu_bar())

	var main_split := VSplitContainer.new()
	main_split.size_flags_vertical = Control.SIZE_EXPAND_FILL
	main_split.split_offset = 650
	main_vbox.add_child(main_split)

	var upper := HBoxContainer.new()
	upper.add_theme_constant_override("separation", 16)
	upper.size_flags_vertical = Control.SIZE_EXPAND_FILL`
);

// 2. Replace _build_menu_bar to add Theme menu
content = content.replace(
    /var project_menu := MenuButton\.new\(\)[\s\S]*?bar\.add_child\(project_menu\)/,
`var project_menu := MenuButton.new()
	project_menu.text = "项目"
	project_menu.get_popup().add_item("新建对话 (Dialogue)…", 1)
	project_menu.get_popup().add_item("新建任务 (Quest)…", 2)
	project_menu.get_popup().add_item("新建剧情节点 (Story Graph)…", 3)
	project_menu.get_popup().add_separator()
	project_menu.get_popup().add_item("构建内容包…", 4)
	project_menu.get_popup().id_pressed.connect(_on_project_menu)
	bar.add_child(project_menu)

	var theme_menu := MenuButton.new()
	theme_menu.text = "主题 (Theme)"
	theme_menu.get_popup().add_item("深空系统紫蓝 (Deep Space)", 1)
	theme_menu.get_popup().add_item("轻科技薄荷蓝 (Mint Tech)", 2)
	theme_menu.get_popup().id_pressed.connect(func(id: int):
		if id == 1:
			ThemeManager.apply_theme("deep_space")
		elif id == 2:
			ThemeManager.apply_theme("mint_tech")
	)
	bar.add_child(theme_menu)`
);

// 3. Replace _build_project_panel to use PanelContainer
content = content.replace(
    /func _build_project_panel\(\) -> Control:\s*var panel := VBoxContainer\.new\(\)/,
`func _build_project_panel() -> Control:
	var card := PanelContainer.new()
	card.custom_minimum_size.x = 280
	var panel := VBoxContainer.new()
	card.add_child(panel)`
);
content = content.replace(
    /panel\.add_child\(project_tree\)\s*return panel/,
`panel.add_child(project_tree)
	return card`
);

// 4. Replace _build_workspace_panel to use PanelContainer
content = content.replace(
    /func _build_workspace_panel\(\) -> Control:\s*var panel := VBoxContainer\.new\(\)/,
`func _build_workspace_panel() -> Control:
	var card := PanelContainer.new()
	card.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	var panel := VBoxContainer.new()
	card.add_child(panel)`
);
content = content.replace(
    /panel\.add_child\(actor_list\)\s*return panel/,
`panel.add_child(actor_list)
	return card`
);

// 5. Replace _build_inspector_panel to use PanelContainer
content = content.replace(
    /func _build_inspector_panel\(\) -> Control:\s*var panel := VBoxContainer\.new\(\)/,
`func _build_inspector_panel() -> Control:
	var card := PanelContainer.new()
	card.custom_minimum_size.x = 380
	var panel := VBoxContainer.new()
	card.add_child(panel)`
);
content = content.replace(
    /tags_edit\.text_changed\.connect\(_mark_dirty_text\)\s*/,
`tags_edit.text_changed.connect(_mark_dirty_text)
	return card
`
);

fs.writeFileSync(mainGdPath, content, 'utf8');
console.log("Updated Main.gd");

// Update other editor windows to use ThemeManager.get_theme() instead of build_modern_theme()
const otherFiles = [
    "DialogueEditorWindow.gd",
    "QuestEditorWindow.gd",
    "StoryEditorWindow.gd",
    "LiveWorkspace.gd"
];

for (const file of otherFiles) {
    const fpath = path.join(baseDir, file);
    if (fs.existsSync(fpath)) {
        let fcontent = fs.readFileSync(fpath, 'utf8');
        fcontent = fcontent.replace(
            /ThemeManager\.build_modern_theme\(\)/g,
            "ThemeManager.get_theme()"
        );
        
        // Also wrap their main VBoxContainer in a PanelContainer if not already done,
        // Wait, for child windows, their root is usually added directly. 
        // Window nodes have a single child usually, so wrapping their root in PanelContainer makes it look like a nice card.
        // I will just let their root inherit the Window panel stylebox which we already styled!
        
        fs.writeFileSync(fpath, fcontent, 'utf8');
        console.log("Updated " + file);
    }
}
