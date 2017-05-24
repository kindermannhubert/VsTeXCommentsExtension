using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using Microsoft.Build.Framework.XamlTypes;
using VsTeXCommentsExtension.Integration.Data;
using VsTeXCommentsExtension.Integration.View;
using VsTeXCommentsExtension.SyntaxHighlighting;
using VsTeXCommentsExtension.View;

namespace VsTeXCommentsExtension.Integration
{
    [Export(typeof(IWpfTextViewConnectionListener))]
    [Export(typeof(WpfTextViewResources))]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Interactive)]
    internal class WpfTextViewResources : IWpfTextViewConnectionListener
    {
        private static IRenderingManager renderingManager;

        private readonly object syncRoot = new object();
        private readonly Dictionary<IWpfTextView, TextViewData> textViews = new Dictionary<IWpfTextView, TextViewData>();
        private readonly Dictionary<ITextBuffer, TextBufferData> textBuffers = new Dictionary<ITextBuffer, TextBufferData>();

        [Import]
        private IBufferTagAggregatorFactoryService BufferTagAggregatorFactoryService = null; //MEF

        [Import]
        private IClassificationTypeRegistryService ClassificationRegistry = null; //MEF

        public WpfTextViewResources()
        {
            if (renderingManager == null)
            {
                lock (textViews)
                {
                    if (renderingManager == null)
                    {
                        renderingManager = new RenderingManager(new HtmlRenderer());
                    }
                }
            }
        }

        public TeXCommentAdornmentTagger GetTeXCommentAdornmentTagger(IWpfTextView textView) => GetOrAddTextViewData(textView)?.GetTexViewData<TeXCommentAdornmentTagger>();
        public TeXSyntaxClassifier GetTeXSyntaxClassifier(ITextBuffer buffer) => GetOrAddTextBufferData(buffer)?.GetTextBufferData<TeXSyntaxClassifier>();
        public TeXCommentTagger GetTeXCommentTagger(ITextBuffer buffer) => GetOrAddTextBufferData(buffer)?.GetTextBufferData<TeXCommentTagger>();

        private TextBufferData GetOrAddTextBufferData(ITextBuffer buffer)
        {
            lock (syncRoot)
            {
                if (buffer.ContentType.TypeName != "Basic" && buffer.ContentType.TypeName != "CSharp" && buffer.ContentType.TypeName != "F#" && buffer.ContentType.TypeName != "C/C++") return null;

                TextBufferData textBufferData;
                if (!textBuffers.TryGetValue(buffer, out textBufferData))
                {
                    textBufferData = new TextBufferData();
                    textBufferData.RegisterTextBufferData(new TeXCommentTagger(buffer));
                    textBufferData.RegisterTextBufferData(new TeXSyntaxClassifier(buffer, ClassificationRegistry));

                    textBuffers.Add(buffer, textBufferData);
                }
                return textBufferData;
            }
        }

        private TextViewData GetOrAddTextViewData(IWpfTextView textView)
        {
            lock (syncRoot)
            {
                if (textView.TextBuffer.ContentType.TypeName != "Basic" && textView.TextBuffer.ContentType.TypeName != "CSharp" && textView.TextBuffer.ContentType.TypeName != "F#" && textView.TextBuffer.ContentType.TypeName != "C/C++") return null;

                TextViewData textViewData;
                if (!textViews.TryGetValue(textView, out textViewData))
                {
                    textView.Closed += TextView_Closed;
                    textViewData = new TextViewData();
                    textViewData.RegisterTextViewData(new TeXCommentAdornmentTagger(textView, renderingManager, BufferTagAggregatorFactoryService.CreateTagAggregator<TeXCommentTag>(textView.TextBuffer)));

                    textViews.Add(textView, textViewData);
                }
                return textViewData;
            }
        }

        void IWpfTextViewConnectionListener.SubjectBuffersConnected(IWpfTextView textView, ConnectionReason reason, Collection<ITextBuffer> subjectBuffers)
        {
            lock (syncRoot)
            {
                if (GetOrAddTextViewData(textView) == null) return;

                foreach (var buffer in subjectBuffers)
                {
                    var textBufferData = GetOrAddTextBufferData(buffer);
                    textBufferData.ConnectToTextView(textView);
                }
            }
        }

        void IWpfTextViewConnectionListener.SubjectBuffersDisconnected(IWpfTextView textView, ConnectionReason reason, Collection<ITextBuffer> subjectBuffers)
        {
            lock (syncRoot)
            {
                foreach (var buffer in subjectBuffers)
                {
                    TextBufferData textBufferData;
                    if (textBuffers.TryGetValue(buffer, out textBufferData))
                    {
                        textBufferData.DisconnectFromTextView(textView);
                    }
                }
            }
        }

        private void TextView_Closed(object sender, EventArgs e)
        {
            lock (syncRoot)
            {
                var textView = (IWpfTextView)sender;
                textView.Closed -= TextView_Closed;
                textViews[textView].Dispose();
                renderingManager.DiscartRenderingRequestsForTextView(textView);
                textViews.Remove(textView);
            }
        }

        private class TextViewData : IDisposable
        {
            private readonly Dictionary<Type, IDisposable> dataRegistrations = new Dictionary<Type, IDisposable>();

            public void RegisterTextViewData<T>(T data)
                where T : class, IDisposable
            {
                dataRegistrations.Add(typeof(T), data);
            }

            public T GetTexViewData<T>()
                where T : class, IDisposable
            {
                IDisposable value;
                return dataRegistrations.TryGetValue(typeof(T), out value) ? (T)value : null;
            }

            public void Dispose()
            {
                foreach (var data in dataRegistrations.Values)
                {
                    data.Dispose();
                }
                dataRegistrations.Clear();
            }
        }

        private class TextBufferData
        {
            private readonly HashSet<IWpfTextView> connectedTextViews = new HashSet<IWpfTextView>();
            private readonly Dictionary<Type, IDisposable> dataRegistrations = new Dictionary<Type, IDisposable>();

            public void RegisterTextBufferData<T>(T data)
                where T : class, IDisposable
            {
                dataRegistrations.Add(typeof(T), data);
            }

            public void ConnectToTextView(IWpfTextView textView)
            {
                connectedTextViews.Add(textView);
            }

            public void DisconnectFromTextView(IWpfTextView textView)
            {
                connectedTextViews.Remove(textView);

                if (connectedTextViews.Count == 0)
                {
                    foreach (var data in dataRegistrations.Values)
                    {
                        data.Dispose();
                    }
                    dataRegistrations.Clear();
                }
            }

            public T GetTextBufferData<T>()
                where T : class, IDisposable
            {
                IDisposable value;
                return dataRegistrations.TryGetValue(typeof(T), out value) ? (T)value : null;
            }
        }
    }
}