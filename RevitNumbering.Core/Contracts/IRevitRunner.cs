namespace RevitNumbering.Core.Contracts
{
    // Runs an action in the Revit API context. The window and Revit share one thread, so the action
    // waits until Revit is free. Only one action can wait at a time.
    // Use the Revit API only inside such an action or inside a Revit event handler.
    public interface IRevitRunner
    {
        // Returns false when Revit refuses the request. The action will not run then.
        bool Run(Action action);

        // Drops the waiting action if Revit has not started it yet. Returns true if it was dropped.
        bool CancelPending();
    }
}
