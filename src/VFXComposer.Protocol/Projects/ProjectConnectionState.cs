namespace VFXComposer.Protocol.Projects;

/// <summary>Connection presentation state; it carries no project path or authority.</summary>
public enum ProjectConnectionState
{
    Disconnected = 0,
    Connecting = 1,
    ConnectedNoRegisteredProject = 2,
    ConnectedRegisteredProject = 3,
    Faulted = 4,
}
