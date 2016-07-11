using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace VsTeXCommentsExtension.View
{
    public class RenderingManager : RenderingManager<HtmlRenderer.Input, RendererResult>, IRenderingManager
    {
        private readonly Queue<Request> tempQueue = new Queue<Request>();

        public RenderingManager(IRenderer<HtmlRenderer.Input, RendererResult> renderer)
            : base(renderer)
        {
        }

        protected override void OnRequestAddition(Queue<Request> queue, HtmlRenderer.Input newRequest)
        {
            //We want to remove already existing requests for same tag adornment (we are interested only in the last one).

            while (queue.Count > 0)
            {
                var existingRequest = queue.Dequeue();
                if (existingRequest.Input.TagAdornment.Index != newRequest.TagAdornment.Index)
                {
                    tempQueue.Enqueue(existingRequest);
                }
            }

            while (tempQueue.Count > 0)
            {
                queue.Enqueue(tempQueue.Dequeue());
            }
        }
    }

    public class RenderingManager<TInput, TResult> : IRenderingManager<TInput, TResult>
        where TInput : IRendererInput
    {
        private readonly IRenderer<TInput, TResult> renderer;
        private readonly Queue<Request> requests = new Queue<Request>();
        private readonly Queue<Request> tempQueue = new Queue<Request>();
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
                OnRequestAddition(requests, input);
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

        protected virtual void OnRequestAddition(Queue<Request> queue, TInput newRequest)
        {
        }

        public void DiscartRenderingRequestsForTextView(ITextView textView)
        {
            lock (requests)
            {
                while (requests.Count > 0)
                {
                    var request = requests.Dequeue();
                    if (request.Input.TextView != textView)
                    {
                        tempQueue.Enqueue(request);
                    }
                }

                while (tempQueue.Count > 0)
                {
                    requests.Enqueue(tempQueue.Dequeue());
                }
            }
        }

        protected struct Request
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