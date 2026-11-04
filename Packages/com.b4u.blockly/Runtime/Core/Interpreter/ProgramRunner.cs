using System;
using System.Diagnostics;

namespace Blockly
{
    public enum RunnerState { Idle, Running, Paused, Finished }

    /// <summary>
    /// Drives an <see cref="Interpreter"/> the way a playground needs: Run (continuous, time-sliced per frame),
    /// Step (execute one statement at a time, pausing on the block that is about to run), Stop, and an optional
    /// slow mode that spaces statements out so the highlighted block is visible.
    /// Call <see cref="Pump"/> once per frame. Pure C#, so it is testable without Unity.
    /// </summary>
    public sealed class ProgramRunner
    {
        readonly Workspace workspace;
        readonly HandlerRegistry handlers;
        Interpreter interpreter;
        bool blockStarted;
        float slowTimer;

        public ProgramRunner(Workspace workspace, HandlerRegistry handlers = null)
        {
            this.workspace = workspace;
            this.handlers = handlers;
        }

        /// <summary>A line produced by text_print.</summary>
        public event Action<string> Output;
        /// <summary>The block about to execute changed (null when nothing is executing).</summary>
        public event Action<Block> CurrentBlockChanged;
        /// <summary>The program ended on its own. Argument is the error message, or null on success.</summary>
        public event Action<string> Finished;

        public RunnerState State { get; private set; } = RunnerState.Idle;
        public Block CurrentBlock { get; private set; }
        public string Error { get; private set; }

        /// <summary>Space statements out by <see cref="SlowInterval"/> seconds when running.</summary>
        public bool Slow;
        public float SlowInterval = 0.25f;
        /// <summary>Per-frame time budget for continuous running, so the UI never freezes.</summary>
        public double BudgetMs = 8;
        /// <summary>Upper bound for one Step press (a statement-less infinite loop must not hang the UI).</summary>
        public double StepBudgetMs = 50;

        public bool IsActive => State == RunnerState.Running || State == RunnerState.Paused;
        public Interpreter Interpreter => interpreter;

        // ---------------------------------------------------------------- commands
        public void Run()
        {
            if (!IsActive) Begin();
            State = RunnerState.Running;
            slowTimer = 0;
        }

        public void Step()
        {
            if (!IsActive) Begin();
            State = RunnerState.Paused;
            AdvanceToNextBlock(StepBudgetMs);
        }

        public void Pause()
        {
            if (State == RunnerState.Running) State = RunnerState.Paused;
        }

        public void Stop()
        {
            if (!IsActive) return;
            interpreter?.Stop();
            State = RunnerState.Idle;
            SetCurrent(null);
        }

        /// <summary>Advances a running program; call every frame.</summary>
        public void Pump(float deltaSeconds)
        {
            if (State != RunnerState.Running) return;
            if (Slow)
            {
                slowTimer -= deltaSeconds;
                if (slowTimer > 0) return;
                slowTimer = SlowInterval;
                AdvanceToNextBlock(BudgetMs);
            }
            else
            {
                interpreter.RunFor(BudgetMs);
                CheckFinished();
            }
        }

        // ---------------------------------------------------------------- internals
        void Begin()
        {
            interpreter = new Interpreter(workspace, handlers) { Print = line => Output?.Invoke(line) };
            interpreter.BlockExecuting += b => { blockStarted = true; SetCurrent(b); };
            Error = null;
            interpreter.Start();
            State = RunnerState.Paused;
        }

        /// <summary>Runs until the next statement is about to execute (or the program ends / the budget is spent).</summary>
        void AdvanceToNextBlock(double budgetMs)
        {
            blockStarted = false;
            var sw = Stopwatch.StartNew();
            while (interpreter.IsRunning && !blockStarted)
            {
                interpreter.Step();
                if (sw.Elapsed.TotalMilliseconds >= budgetMs) break;
            }
            CheckFinished();
        }

        void CheckFinished()
        {
            if (interpreter.IsRunning) return;
            Error = interpreter.Error;
            State = RunnerState.Finished;
            SetCurrent(null);
            Finished?.Invoke(Error);
        }

        void SetCurrent(Block b)
        {
            if (ReferenceEquals(CurrentBlock, b)) return;
            CurrentBlock = b;
            CurrentBlockChanged?.Invoke(b);
        }
    }
}
