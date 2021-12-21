using System;
using System.Collections.Generic;
using System.Threading;
using static ActionRecorder.Application;

namespace ActionRecorder.scheduler
{
    public class Scheduler
    {
        public static long TaskId = -1;
        private Dictionary<long, Task> _queue = new Dictionary<long, Task>();
        private Thread _thread;
        private int _baseDelay => (int) (1000 / Instance.TickRate);
        
        public void Start()
        {
            _thread = new Thread(() =>
            {
                while (Instance.Running)
                {
                    try
                    {
                        Thread.Sleep(_baseDelay);
                        Run();
                    }
                    catch (Exception e)
                    {
                        Instance.Error(e.Message);
                    }
                }
            });
            _thread.Start();
        }

        public void Stop()
        {
            _thread.Abort();
        }

        private void Run()
        {
            foreach (var keyValuePair in _queue)
            {
                Task task = keyValuePair.Value;
                task.Delay -= _baseDelay;
                if (task.Repeat && task.Delay <= 0)
                {
                    task.OnRun();
                    task.Delay = task.MaxDelay;
                } else if (!task.Repeat && task.Delay <= 0)
                {
                    task.OnRun();
                    RemoveTask(keyValuePair.Key);
                }
            }
        }

        public void AddTask(Task task, int delay = 0, bool repeat = false)
        {
            task.Delay = delay;
            task.MaxDelay = delay;
            task.Repeat = repeat;
            task.OnRun();
            _queue.Add(task.TaskId, task);
        }

        public void RemoveTask(long id)
        {
            _queue.Remove(id);
        }

        public void CallAfter(Action callback, int milliseconds)
        {
            System.Threading.Tasks.Task.Delay(milliseconds).ContinueWith(t =>
            {
                callback();
            });
        }
    }
}