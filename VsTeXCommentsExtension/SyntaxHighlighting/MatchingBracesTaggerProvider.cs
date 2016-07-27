﻿//using Microsoft.VisualStudio.Text;
//using Microsoft.VisualStudio.Text.Editor;
//using Microsoft.VisualStudio.Text.Operations;
//using Microsoft.VisualStudio.Text.Tagging;
//using Microsoft.VisualStudio.Utilities;
//using System.ComponentModel.Composition;

//namespace HighlightWordSample
//{
//    /// <summary>
//    /// Export a <see cref="IViewTaggerProvider"/>
//    /// </summary>
//    [Export(typeof(IViewTaggerProvider))]
//    [ContentType("CSharp")]
//    [TagType(typeof(HighlightWordTag))]
//    public class HighlightWordTaggerProvider : IViewTaggerProvider
//    {
//        [Import]
//        private ITextSearchService TextSearchService { get; set; }

//        [Import]
//        private ITextStructureNavigatorSelectorService TextStructureNavigatorSelector { get; set; }

//        /// <summary>
//        /// This method is called by VS to generate the tagger
//        /// </summary>
//        /// <typeparam name="T"></typeparam>
//        /// <param name="textView"> The text view we are creating a tagger for</param>
//        /// <param name="buffer"> The buffer that the tagger will examine for instances of the current word</param>
//        /// <returns> Returns a HighlightWordTagger instance</returns>
//        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
//        {
//            // Only provide highlighting on the top-level buffer
//            if (textView.TextBuffer != buffer)
//                return null;

//            ITextStructureNavigator textStructureNavigator =
//                TextStructureNavigatorSelector.GetTextStructureNavigator(buffer);

//            return new HighlightWordTagger(textView, buffer, TextSearchService, textStructureNavigator) as ITagger<T>;
//        }
//    }
//}