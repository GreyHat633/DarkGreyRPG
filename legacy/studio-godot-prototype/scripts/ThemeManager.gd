class_name ThemeManager

static var active_theme: Theme = null
static var current_theme_name := "deep_space"

static func get_theme() -> Theme:
	if not active_theme:
		active_theme = Theme.new()
		apply_theme(current_theme_name)
	return active_theme

static func apply_theme(theme_name: String) -> void:
	current_theme_name = theme_name
	active_theme.clear()
	
	active_theme.set_default_font_size(15)
	
	var bg_color: Color
	var panel_color: Color
	var border_color: Color
	var text_primary: Color
	var text_secondary: Color
	var accent_color: Color
	var accent_hover: Color
	var accent_pressed: Color
	var input_bg: Color
	var list_bg: Color
	
	if theme_name == "deep_space":
		bg_color = Color("#2B2F4A")
		panel_color = Color("#35395A")
		border_color = Color("#5B5FA6")
		text_primary = Color("#EEF0FF")
		text_secondary = Color("#EEF0FF").lerp(Color(0,0,0,0), 0.4)
		accent_color = Color("#7D8CFF")
		accent_hover = Color("#9DACFF")
		accent_pressed = Color("#6D7CEF")
		input_bg = Color("#1E213A")
		list_bg = Color("#22263C")
	elif theme_name == "mint_tech":
		bg_color = Color("#3C5C5B")
		panel_color = Color("#456867")
		border_color = Color("#8CA9A9") # 冷青灰
		text_primary = Color("#EEF6F6")
		text_secondary = Color("#EEF6F6").lerp(Color(0,0,0,0), 0.4)
		accent_color = Color("#6ED3CF")
		accent_hover = Color("#8DE3DF")
		accent_pressed = Color("#5EC3BF")
		input_bg = Color("#2C4C4B")
		list_bg = Color("#325251")
	else:
		# fallback to a default dark
		bg_color = Color("#1c1c1e")
		panel_color = Color("#2c2c2e")
		border_color = Color("#3a3a3c")
		text_primary = Color("#ffffff")
		text_secondary = Color("#ebebf5").lerp(Color(0,0,0,0), 0.4)
		accent_color = Color("#0a84ff")
		accent_hover = Color("#409cff")
		accent_pressed = Color("#0060cc")
		input_bg = Color("#151515")
		list_bg = Color("#18181a")
	
	# --- StyleBoxes (Luxurious & Minimalist Spacing) ---
	
	# Window / Main Background
	var style_bg := StyleBoxFlat.new()
	style_bg.bg_color = bg_color
	active_theme.set_stylebox("panel", "Panel", style_bg)
	active_theme.set_stylebox("panel", "Window", style_bg)
	
	# Standard Panel Container (Floating Cards)
	var style_panel := StyleBoxFlat.new()
	style_panel.bg_color = panel_color
	style_panel.border_width_bottom = 1
	style_panel.border_width_top = 1
	style_panel.border_width_left = 1
	style_panel.border_width_right = 1
	style_panel.border_color = border_color
	style_panel.corner_radius_top_left = 12
	style_panel.corner_radius_top_right = 12
	style_panel.corner_radius_bottom_left = 12
	style_panel.corner_radius_bottom_right = 12
	style_panel.content_margin_left = 16
	style_panel.content_margin_right = 16
	style_panel.content_margin_top = 16
	style_panel.content_margin_bottom = 16
	style_panel.shadow_color = Color(0, 0, 0, 0.2)
	style_panel.shadow_size = 12
	active_theme.set_stylebox("panel", "PanelContainer", style_panel)
	
	# TabContainer Panel (Lighter)
	var style_tab := style_panel.duplicate() as StyleBoxFlat
	style_tab.content_margin_top = 8
	active_theme.set_stylebox("panel", "TabContainer", style_tab)
	
	# Buttons (Pill / Smooth Rounded)
	var btn_normal := StyleBoxFlat.new()
	btn_normal.bg_color = panel_color.lerp(Color(0,0,0), 0.2)
	btn_normal.corner_radius_top_left = 8
	btn_normal.corner_radius_top_right = 8
	btn_normal.corner_radius_bottom_left = 8
	btn_normal.corner_radius_bottom_right = 8
	btn_normal.content_margin_left = 16
	btn_normal.content_margin_right = 16
	btn_normal.content_margin_top = 8
	btn_normal.content_margin_bottom = 8
	btn_normal.border_width_bottom = 1
	btn_normal.border_color = border_color.lerp(Color(0,0,0,0), 0.5)
	
	var btn_hover := btn_normal.duplicate() as StyleBoxFlat
	btn_hover.bg_color = panel_color.lerp(accent_color, 0.15)
	
	var btn_pressed := btn_normal.duplicate() as StyleBoxFlat
	btn_pressed.bg_color = panel_color.lerp(Color(0,0,0), 0.4)
	btn_pressed.border_width_bottom = 0
	btn_pressed.border_width_top = 1
	
	var btn_disabled := btn_normal.duplicate() as StyleBoxFlat
	btn_disabled.bg_color = panel_color.lerp(Color(0,0,0,0.5), 0.5)
	
	active_theme.set_stylebox("normal", "Button", btn_normal)
	active_theme.set_stylebox("hover", "Button", btn_hover)
	active_theme.set_stylebox("pressed", "Button", btn_pressed)
	active_theme.set_stylebox("disabled", "Button", btn_disabled)
	active_theme.set_color("font_color", "Button", text_primary)
	active_theme.set_color("font_focus_color", "Button", text_primary)
	active_theme.set_color("font_hover_color", "Button", text_primary)
	active_theme.set_color("font_pressed_color", "Button", accent_color)
	active_theme.set_color("font_disabled_color", "Button", text_secondary)
	
	# Header MenuButton
	var menu_normal := StyleBoxEmpty.new()
	menu_normal.content_margin_left = 12
	menu_normal.content_margin_right = 12
	menu_normal.content_margin_top = 8
	menu_normal.content_margin_bottom = 8
	
	var menu_hover := btn_hover.duplicate() as StyleBoxFlat
	menu_hover.bg_color = panel_color.lerp(accent_color, 0.2)
	menu_hover.corner_radius_top_left = 6
	menu_hover.corner_radius_top_right = 6
	menu_hover.corner_radius_bottom_left = 6
	menu_hover.corner_radius_bottom_right = 6
	
	active_theme.set_stylebox("normal", "MenuButton", menu_normal)
	active_theme.set_stylebox("hover", "MenuButton", menu_hover)
	active_theme.set_stylebox("pressed", "MenuButton", menu_hover)
	
	# PopupMenu
	var popup_style := style_panel.duplicate() as StyleBoxFlat
	popup_style.shadow_size = 16
	popup_style.content_margin_left = 4
	popup_style.content_margin_right = 4
	popup_style.content_margin_top = 4
	popup_style.content_margin_bottom = 4
	active_theme.set_stylebox("panel", "PopupMenu", popup_style)
	
	var popup_hover := StyleBoxFlat.new()
	popup_hover.bg_color = accent_color.lerp(panel_color, 0.7)
	popup_hover.corner_radius_top_left = 6
	popup_hover.corner_radius_top_right = 6
	popup_hover.corner_radius_bottom_left = 6
	popup_hover.corner_radius_bottom_right = 6
	active_theme.set_stylebox("hover", "PopupMenu", popup_hover)
	
	# Inputs (LineEdit, TextEdit)
	var input_normal := StyleBoxFlat.new()
	input_normal.bg_color = input_bg
	input_normal.border_width_bottom = 2
	input_normal.border_color = border_color.lerp(Color(0,0,0,0), 0.7)
	input_normal.corner_radius_top_left = 8
	input_normal.corner_radius_top_right = 8
	input_normal.corner_radius_bottom_left = 8
	input_normal.corner_radius_bottom_right = 8
	input_normal.content_margin_left = 12
	input_normal.content_margin_right = 12
	input_normal.content_margin_top = 10
	input_normal.content_margin_bottom = 10
	
	var input_focus := input_normal.duplicate() as StyleBoxFlat
	input_focus.border_color = accent_color
	input_focus.bg_color = input_bg.lerp(accent_color, 0.05)
	
	active_theme.set_stylebox("normal", "LineEdit", input_normal)
	active_theme.set_stylebox("focus", "LineEdit", input_focus)
	active_theme.set_stylebox("normal", "TextEdit", input_normal)
	active_theme.set_stylebox("focus", "TextEdit", input_focus)
	
	# Trees and ItemLists
	var tree_bg := input_normal.duplicate() as StyleBoxFlat
	tree_bg.bg_color = list_bg
	tree_bg.border_width_bottom = 0
	active_theme.set_stylebox("panel", "Tree", tree_bg)
	active_theme.set_stylebox("panel", "ItemList", tree_bg)
	
	var selected_box := StyleBoxFlat.new()
	selected_box.bg_color = accent_color.lerp(Color(0,0,0,0), 0.7)
	selected_box.border_width_left = 4
	selected_box.border_color = accent_color
	selected_box.corner_radius_top_right = 4
	selected_box.corner_radius_bottom_right = 4
	
	var focus_box := StyleBoxEmpty.new()
	active_theme.set_stylebox("focus", "Tree", focus_box)
	active_theme.set_stylebox("focus", "ItemList", focus_box)
	
	active_theme.set_stylebox("selected", "Tree", selected_box)
	active_theme.set_stylebox("selected_focus", "Tree", selected_box)
	active_theme.set_stylebox("selected", "ItemList", selected_box)
	active_theme.set_stylebox("selected_focus", "ItemList", selected_box)
	
	# Labels
	active_theme.set_color("font_color", "Label", text_primary)
	
	# ScrollBars
	var grabber := StyleBoxFlat.new()
	grabber.bg_color = border_color.lerp(Color(1,1,1), 0.2)
	grabber.corner_radius_top_left = 4
	grabber.corner_radius_top_right = 4
	grabber.corner_radius_bottom_left = 4
	grabber.corner_radius_bottom_right = 4
	
	var scroll_bg := StyleBoxFlat.new()
	scroll_bg.bg_color = Color(0, 0, 0, 0)
	scroll_bg.content_margin_left = 6
	scroll_bg.content_margin_right = 6
	
	active_theme.set_stylebox("grabber", "VScrollBar", grabber)
	active_theme.set_stylebox("grabber_highlight", "VScrollBar", grabber.duplicate())
	active_theme.set_stylebox("grabber_pressed", "VScrollBar", grabber.duplicate())
	active_theme.set_stylebox("scroll", "VScrollBar", scroll_bg)
	active_theme.set_stylebox("grabber", "HScrollBar", grabber)
	active_theme.set_stylebox("grabber_highlight", "HScrollBar", grabber.duplicate())
	active_theme.set_stylebox("grabber_pressed", "HScrollBar", grabber.duplicate())
	active_theme.set_stylebox("scroll", "HScrollBar", scroll_bg)
