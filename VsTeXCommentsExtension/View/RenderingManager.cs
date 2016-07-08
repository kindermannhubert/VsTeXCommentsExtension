using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace VsTeXCommentsExtension.View
{
    public class RenderingManager : RenderingManager<HtmlRenderer.Input, RendererResult>, IRenderingManager
    {
        public RenderingManager(IRenderer<HtmlRenderer.Input, RendererResult> renderer)
            : base(renderer)
        {
        }
    }

    public class RenderingManager<TInput, TResult> : IRenderingManager<TInput, TResult>
    {
        private readonly IRenderer<TInput, TResult> renderer;
        private readonly Queue<Request> requests = new Queue<Request>();
        private readonly ManualResetEvent manualResetEvent = new ManualResetEvent(false);

        public RenderingManager(IRenderer<TInput, TResult> renderer)
        {
            this.renderer = renderer;

            var thread = new Thread(ProcessQueue);
            thread.Name = $"{typeof(RenderingManager).FullName}_{nameof(ProcessQueue)}";
            thread.Start();
        }

        public void RenderAsync(TInput input, Action<TResult> renderingDoneCallback)
        {
            lock (requests)
            {
                Debug.WriteLine(nameof(RenderAsync));
                requests.Enqueue(new Request(input, renderingDoneCallback));
                manualResetEvent.Set();
            }
        }

        private void ProcessQueue()
        {
            while (true)
            {
                while (requests.Count > 0)
                {
                    Request request;
                    lock (requests)
                    {
                        request = requests.Dequeue();
                        if (requests.Count == 0) manualResetEvent.Reset();
                    }

                    var result = renderer.Render(request.Input);
                    request.ResultCallback(result);
                }

                manualResetEvent.WaitOne();
            }
        }

        private struct Request
        {
            public readonly TInput Input;
            public readonly Action<TResult> ResultCallback;

            public Request(TInput input, Action<TResult> resultCallback)
            {
                Input = input;
                ResultCallback = resultCallback;
            }
        }
    }
}