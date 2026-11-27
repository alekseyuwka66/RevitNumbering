namespace RevitNumbering.Core.Contracts
{
    // Called after each batch of elements is written. Revit and the window share one thread,
    // so the window can redraw and read a click on Cancel only here.
    public interface IWriteProgress
    {
        // Returns false to cancel: the writer rolls back everything it has written.
        bool Continue(int done, int total);
    }
}
