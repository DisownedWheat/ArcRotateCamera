using Godot;
using System;

public class CameraHolder : Spatial
{
    // Signal for notifying that an event triggered a collision
    [Signal]
    public delegate void CollisionEncountered(ObjectWrapper trigger, Vector3 position, Godot.Node collider, Vector3 normal);

    // Handles how quickly to rotate
    [Export]
    public float RotationSensitivity = 200;

    // Define minimum and maximum zoom levels
    [Export(PropertyHint.Range, "0,10,1")]
    public int MinZoom = 4;
    [Export(PropertyHint.Range, "0,100,1")]
    public int MaxZoom = 10;

    //How much the zoom changes each scroll wheel tick
    [Export(PropertyHint.Range, "1,10,1")]
    public int ZoomTravel = 1;

    // Adjusts the power of the lerp
    [Export(PropertyHint.Range, "0,1")]
    public float ZoomLerp = 1;

    // Defines how much the camera can rotate on the Y axis
    [Export(PropertyHint.Range, "0,180,1")]
    public int MaxYRotation = 60;

    // Sets the target node that the position wil lerp to, otherwise if it is the
    // child of a node it will inherit the position
    [Export]
    public NodePath PositionTarget;

    [Export]
    public float PositionTargetOffset = 1f;

    // Defines how strong the positonal lerp is
    [Export(PropertyHint.Range, "0,1")]
    public float PositionLerp = .1f;

    // Defines the rotational lerp
    [Export(PropertyHint.Range, "0,1")]
    public float RotationLerp = .1f;

    // Defines whether we use the listed actions in the map or just the default
    // keycodes on events
    [Export]
    public Boolean UseActions = true;

    // Defines the actions the camera will use for input
    [Export]
    public Godot.Collections.Dictionary<String, String> ActionMap = CameraHolder.GenerateDefaultInputMap();

    // The node the camera will follow if not the direct parent node
    private Spatial TargetNode;
    // Handles checks for having the "button" down for camera rotation
    private Boolean MiddleMouseDown = false;
    // Reference to the actual camera
    private Camera Camera;

    // Rotation
    private float RotX = 0f;
    private float RotY = 0f;
    private Vector3 TargetRotation;

    // Zoom
    private float ZoomLevel;
    private float TargetZoom;

    // Reference to Raycast object
    private RayCast Ray;

    private Boolean RotationChanged = false;

    private ObjectWrapper EventTrigger = null;

    private static Godot.Collections.Dictionary<string, string> GenerateDefaultInputMap()
    {
        var dict = new Godot.Collections.Dictionary<String, String>();
        dict.Add("ZoomIn", "zoom_in");
        dict.Add("ZoomOut", "zoom_out");
        dict.Add("MiddleClick", "middle_click");
        return dict;
    }

    public override void _Ready()
    {
        // Set all the initial variables
        Camera = GetNode<Camera>("Camera");
        Ray = GetNode<RayCast>("Camera/RayCast");
        ZoomLevel = MinZoom;
        TargetZoom = MinZoom;
        TargetRotation = GlobalRotation;

        ZoomLerp = (float)ZoomLerp / 10;
        if (PositionTarget != null)
        {
            TargetNode = GetNode<Spatial>(PositionTarget);
        }
    }

    public override void _Process(float delta)
    {
        ZoomLevel = Mathf.Lerp(ZoomLevel, TargetZoom, ZoomLerp);
        var t = Camera.Translation;
        t.y = ZoomLevel;
        Camera.Translation = t;

        if (TargetNode != null)
        {
            var targetPosition = TargetNode.GlobalTranslation;
            targetPosition.y += PositionTargetOffset;
            GlobalTranslation = GlobalTranslation.LinearInterpolate(targetPosition, PositionLerp);
        }

        var rot = Rotation;
        rot.y = Mathf.LerpAngle(rot.y, TargetRotation.y, 0.1f);
        rot.x = Mathf.LerpAngle(rot.x, TargetRotation.x, 0.1f);
        Rotation = rot;
    }

    public override void _PhysicsProcess(float delta)
    {
        if (Ray.CastTo != Vector3.Zero)
        {
            var collider = Ray.GetCollider();
            var position = Ray.GetCollisionPoint();
            var normal = Ray.GetCollisionNormal();
            EmitSignal(nameof(CameraHolder.CollisionEncountered), EventTrigger, position, collider, normal);
            Ray.CastTo = Vector3.Zero;
            EventTrigger = null;
        }
    }

    public override void _Input(InputEvent @event)
    {
        // Handle all the basic inputs
        if (!UseActions)
        {
            HandleNonActionInput(@event);
        }
        else
        {
            if (@event.IsActionPressed(ActionMap["ZoomIn"]))
            {
                TargetZoom = Mathf.Clamp(ZoomLevel - ZoomTravel, MinZoom, MaxZoom);
            }
            if (@event.IsActionPressed(ActionMap["ZoomOut"]))
            {
                TargetZoom = Mathf.Clamp(ZoomLevel + ZoomTravel, MinZoom, MaxZoom);
            }
            if (@event.IsActionPressed(ActionMap["MiddleClick"]))
            {
                MiddleMouseDown = true;
            }
            if (@event.IsActionReleased(ActionMap["MiddleClick"]))
            {
                MiddleMouseDown = false;
            }
        }

        // Now handle the mouse movement
        if (MiddleMouseDown && @event is InputEventMouseMotion)
        {
            var e = (InputEventMouseMotion)@event;
            TargetRotation.x += e.Relative.y / RotationSensitivity;
            TargetRotation.y += e.Relative.x / RotationSensitivity;

            var x = Mathf.Rad2Deg(TargetRotation.x);
            x = Mathf.Clamp(x, 0, (MaxYRotation));
            TargetRotation.x = Mathf.Deg2Rad(x);
        }
    }

    private void HandleNonActionInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton && ((InputEventMouseButton)@event).ButtonIndex == (int)ButtonList.WheelUp)
        {
            TargetZoom = Mathf.Clamp(ZoomLevel - ZoomTravel, MinZoom, MaxZoom);
        }
        if (@event is InputEventMouseButton && ((InputEventMouseButton)@event).ButtonIndex == (int)ButtonList.WheelDown)
        {
            TargetZoom = Mathf.Clamp(ZoomLevel + ZoomTravel, MinZoom, MaxZoom);
        }
        if (
            @event is InputEventMouseButton &&
            ((InputEventMouseButton)@event).ButtonIndex == (int)ButtonList.Middle &&
            @event.IsPressed())
        {
            MiddleMouseDown = true;
        }
        if (
            @event is InputEventMouseButton &&
            ((InputEventMouseButton)@event).ButtonIndex == (int)ButtonList.Middle &&
            !@event.IsPressed())
        {
            MiddleMouseDown = false;
        }
    }

    public void SetRayTarget(object trigger, Vector2 mousePosition)
    {
        if (EventTrigger != null)
        {
            return;
        }
        var target = Camera.ProjectLocalRayNormal(mousePosition);
        Ray.CastTo = target * 1000;
        EventTrigger = new ObjectWrapper(trigger);
    }
}
