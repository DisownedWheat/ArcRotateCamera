extends Spatial

signal collision_encountered(trigger, position, collider, normal)

# Handles how quickly to rotate
export (float) var rotation_sensitivity = 200

# Define minimum and maximum zoom levels
export (int, 10) var min_zoom = 4
export (int, 100) var max_zoom = 10

# How much the zoom changes each scroll wheel tick
export (int, 10) var zoom_travel = 1

# Adjusts the power of the lerp
export (float, 0, 1) var zoom_lerp = 1

# Defines how much the camera can rotate on the Y axis
export (int, 0, 180) var max_x_rotation = 60

# Sets a target node that the position will lerp to, otherwise
# if it is the child of a node it will inherit the position
export (NodePath) var position_target: NodePath = ''
export (float, 0, 1) var position_lerp = 1
var target_node: Spatial

# Defines the rotational lerp
export (float, 0, 1) var rotation_lerp = .1

# Defines whether we use the listed actions in the map or just default
# key codes on events
export var use_actions: bool = true
export (Dictionary) var action_map: Dictionary = {
	"ZoomIn": "zoom_in",
	"ZoomOut": "zoom_out",
	"MiddleClick": "middle_click"
}

var middle_mouse_down: bool = false
onready var camera: Camera = $Camera
onready var ray: RayCast = $Camera/RayCast

# Rotation
onready var target_rotation: Vector3 = rotation

# Zoom
onready var zoom_level: float = min_zoom
onready var target_zoom: float = min_zoom

# Container for returning what triggered the collision signal
var event_trigger = null

func _ready() -> void:
	zoom_lerp = float(zoom_lerp) / 10
	if position_target:
		target_node = get_node(position_target)
		print(target_node)

func _process(delta: float) -> void:
	zoom_level = lerp(zoom_level, target_zoom, zoom_lerp)
	camera.transform.origin.y = zoom_level

	if target_node:
		global_translation = global_translation.linear_interpolate(target_node.global_translation, position_lerp)

	rotation.y = lerp_angle(rotation.y, target_rotation.y, rotation_lerp)
	rotation.x = lerp_angle(rotation.x, target_rotation.x, rotation_lerp)

func _physics_process(delta: float) -> void:
	if ray.cast_to != Vector3.ZERO:
		var collider = ray.get_collider()
		var position = ray.get_collision_point()
		var normal = ray.get_collision_normal()
		emit_signal("collision_encountered", event_trigger, position, collider, normal)
		ray.cast_to = Vector3.ZERO
		event_trigger = null

func _input(event: InputEvent) -> void:
	if not use_actions:
		_handle_non_action_input(event)
	else:
		if event.is_action_pressed(action_map.ZoomIn):
			target_zoom = clamp(zoom_level - zoom_travel, min_zoom, max_zoom)

		if event.is_action_pressed(action_map.ZoomOut):
			target_zoom = clamp(zoom_level + zoom_travel, min_zoom, max_zoom)

		if event.is_action_pressed(action_map.MiddleClick):
			middle_mouse_down = true
		if event.is_action_released(action_map.MiddleClick):
			middle_mouse_down = false

	if middle_mouse_down and event is InputEventMouseMotion:
		target_rotation.y += event.relative.x / rotation_sensitivity
		target_rotation.x += event.relative.y / rotation_sensitivity
		var x = clamp(rad2deg(target_rotation.x), 0, max_x_rotation)
		target_rotation.x = deg2rad(x)


func _handle_non_action_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.button_index == BUTTON_WHEEL_UP:
			target_zoom = clamp(zoom_level - zoom_travel, min_zoom, max_zoom)
	if event is InputEventMouseButton and event.button_index == BUTTON_WHEEL_DOWN:
			target_zoom = clamp(zoom_level + zoom_travel, min_zoom, max_zoom)
	if event is InputEventMouseButton and event.button_index == BUTTON_MIDDLE and event.pressed:
		middle_mouse_down = true
	if event is InputEventMouseButton and event.button_index == BUTTON_MIDDLE and not event.pressed:
		middle_mouse_down = false

func set_ray_target(trigger, mouse_position: Vector2):
	if event_trigger != null:
		return

	var target = camera.project_local_ray_normal(mouse_position)
	ray.cast_to = target * 1000;
	event_trigger = trigger
