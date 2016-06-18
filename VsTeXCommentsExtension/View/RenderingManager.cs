using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Media.Imaging;

namespace VsTeXCommentsExtension.View
{
    public class RenderingManager : RenderingManager<BitmapSource>, IRenderingManager
    {
        public RenderingManager(IRenderer<BitmapSource> renderer)
            : base(renderer)
        {
        }
    }

    public class RenderingManager<TResult> : IRenderingManager<TResult>
    {
        private readonly IRenderer<TResult> renderer;
        private readonly Queue<Request> requests = new Queue<Request>();
        private readonly ManualResetEvent manualResetEvent = new ManualResetEvent(false);

        public RenderingManager(IRenderer<TResult> renderer)
        {
            this.renderer = renderer;

            var thread = new Thread(ProcessQueue);
            thread.Name = $"{typeof(RenderingManager).FullName}_{nameof(ProcessQueue)}";
            thread.Start();
        }

        public void LoadContentAsync(string content, Action<TResult> renderingDoneCallback)
        {
            lock (requests)
            {
                requests.Enqueue(new Request(content, renderingDoneCallback));
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

                    var result = renderer.Render(request.Content);
                    request.ResultCallback(result);
                }

                manualResetEvent.WaitOne();
            }
        }

        private struct Request
        {
            public readonly string Content;
            public readonly Action<TResult> ResultCallback;

            public Request(string content, Action<TResult> resultCallback)
            {
                Content = content;
                ResultCallback = resultCallback;
            }
        }
    }
}