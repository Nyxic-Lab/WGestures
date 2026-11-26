using System;
using System.Diagnostics;
using WGestures.Common.OsSpecific.Windows;
using WGestures.Core.Commands;

namespace WGestures.Core
{
    /// <summary>
    /// Gesture + Command + Context
    /// </summary>
    [Serializable]
    public class GestureIntent
    {
        public class ExecutionResult
        {
            public Exception Exception { get; private set; }
            public bool IsOk { get; private set; }

            public ExecutionResult(Exception e, bool isOk)
            {
                this.Exception = e;
                this.IsOk = isOk;
            }
        }

        public Gesture Gesture { get; set; }
        public AbstractCommand Command { get; set; }

        public bool ExecuteOnModifier { get; set; }

        public bool CanExecuteOnModifier()
        {
            return Gesture.Modifier != GestureModifier.None && ExecuteOnModifier;
        }


        public string Name { get; set; }

        internal void PrepareCommand(GestureContext context, GestureParser gestureParser)
        {
            if (Command == null) throw new InvalidOperationException("Gesture has no command.");
            var contextAware = Command as IGestureContextAware;
            if (contextAware != null) contextAware.Context = context;
            var parserAware = Command as IGestureParserAware;
            if (parserAware != null) parserAware.Parser = gestureParser;
            var shouldInit = Command as INeedInit;
            if (shouldInit != null && !shouldInit.IsInitialized) shouldInit.Init();
        }

        public ExecutionResult Execute(GestureContext context, GestureParser gestureParser)
        {
            try
            {
                PrepareCommand(context, gestureParser);
                if (context != null) context.ActivateTargetWindow();
                Command.Execute();
                // Let the runtime manage its working set instead of trimming after every gesture.
                return new ExecutionResult(null, true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Gesture command failed: " + ex);
                return new ExecutionResult(ex, false);
            }
        }
    }
}
