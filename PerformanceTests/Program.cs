using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using VsTeXCommentsExtension.Integration.Data;

namespace PerformanceTests
{
    class Program
    {
        static void Main()
        {
            var codeToWrite = RandomCodeGenerator.GenerateCode(segmentsCount: 150).Select(c => c.ToString()).ToArray(); //cca 2k lines

            //Load referenced assembly
            typeof(IContentType).ToString();

            const string VsEditorAssemblyPath = @"C:\Program Files (x86)\Microsoft Visual Studio\2017\Professional\Common7\IDE\CommonExtensions\Microsoft\Editor";
            var vsAssembliesPaths = new[]
            {
                VsEditorAssemblyPath,
                @"C:\Program Files (x86)\Microsoft Visual Studio\2017\Professional\Common7\IDE\PrivateAssemblies"
                //@"C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview\Common7\IDE\CommonExtensions\Microsoft\Editor";
            };
            AppDomain.CurrentDomain.AssemblyResolve +=
                (object sender, ResolveEventArgs args) =>
                {
                    var shortName = GetShortName(args.Name);

                    //need to use assemblies referenced by this project and not by VS assemblies
                    var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
                    var loadedAssembly = loadedAssemblies.Where(a => GetShortName(a.FullName) == shortName).SingleOrDefault();
                    if (loadedAssembly != null) return loadedAssembly;

                    foreach (var vsAssembliesPath in vsAssembliesPaths)
                    {
                        var path = Path.Combine(vsAssembliesPath, shortName + ".dll");
                        if (File.Exists(path)) return Assembly.LoadFile(path);
                    }
                    throw new InvalidOperationException($"Unable to find assembly '{args.Name}'.");

                    string GetShortName(string assemblyFullName) => assemblyFullName.Split(',')[0];
                };

            var vsEditorAssembly = Assembly.LoadFile(Path.Combine(VsEditorAssemblyPath, "Microsoft.VisualStudio.Platform.VSEditor.dll"));
            var contentType = CreateContentType(vsEditorAssembly, "CSharp");
            var stringRebuilder = CreateStringRebuilder(vsEditorAssembly, string.Empty);
            var guardedOperations = CreateGuardedOperations(vsEditorAssembly);
            var textBuffer = CreateTextBuffer(vsEditorAssembly, contentType, stringRebuilder, null, guardedOperations);
            var teXCommentTagger = new TeXCommentTagger(textBuffer);

            var watch = new Stopwatch();
            Console.WriteLine($"{nameof(TeXCommentTagger)} performance:");

            (double elapsedMs, double allocatedMemory, double gcCollectionsCount) baseLine = (0, 0, 0);
#if DEBUG
            const int BaselineIterations = 1;
#else
            const int BaselineIterations = 100; 
#endif
            for (int iteration = 0; iteration < BaselineIterations; iteration++)
            {
                var result = Test(codeToWrite, textBuffer, teXCommentTagger, watch, taggingEnabled: false);
                baseLine.elapsedMs += result.elapsedMs;
                baseLine.allocatedMemory += result.allocatedMemory;
                baseLine.gcCollectionsCount += result.gcCollectionsCount;
            }
            baseLine.elapsedMs /= BaselineIterations;
            baseLine.allocatedMemory /= BaselineIterations;
            baseLine.gcCollectionsCount /= BaselineIterations;

            Console.WriteLine($"Baseline (no tagging): { baseLine.elapsedMs}ms\tGC: { baseLine.gcCollectionsCount}\tTotalMemory:{ baseLine.allocatedMemory }");
            for (int iteration = 0; iteration < 10; iteration++)
            {
                var (elapsedTime, allocatedMemory, gcCollectionsCount) = Test(codeToWrite, textBuffer, teXCommentTagger, watch, taggingEnabled: true);
                Console.WriteLine($"{elapsedTime - baseLine.elapsedMs}ms\tGC: {gcCollectionsCount - baseLine.gcCollectionsCount}\tTotalMemory:{allocatedMemory - baseLine.allocatedMemory }");
            }
        }

        private static (double elapsedMs, double allocatedMemory, double gcCollectionsCount) Test(
            string[] codeToWrite,
            ITextBuffer textBuffer,
            TeXCommentTagger teXCommentTagger,
            Stopwatch watch,
            bool taggingEnabled)
        {
            textBuffer.Delete(new Span(0, textBuffer.CurrentSnapshot.Length));

            var totalMemory = GC.GetTotalMemory(true);
            var gcCount = GcCount();

            watch.Restart();
            for (int i = 0; i < codeToWrite.Length; i++)
            {
                textBuffer.Insert(textBuffer.CurrentSnapshot.Length, codeToWrite[i]);
                var spans = new NormalizedSnapshotSpanCollection(textBuffer.CurrentSnapshot, new Span(0, textBuffer.CurrentSnapshot.Length));

                if (taggingEnabled)
                {
                    var tags = teXCommentTagger.GetTags(spans);
                    var count = 0;
                    foreach (var tag in tags) ++count;
                }
            }

            return (watch.ElapsedMilliseconds, GC.GetTotalMemory(false) - totalMemory, GcCount() - gcCount);
        }

        private static IContentType CreateContentType(Assembly vsEditorAssembly, string contentType)
        {
            var type = vsEditorAssembly.GetType("Microsoft.VisualStudio.Utilities.Implementation.ContentTypeImpl");
            var ctor = type.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new Type[] { typeof(string), typeof(string), typeof(IEnumerable<string>) },
                null);
            return (IContentType)ctor.Invoke(new[] { contentType, null, null }); //string name, string mimeType = null, IEnumerable<string> baseTypes = null
        }

        private static object CreateStringRebuilder(Assembly vsEditorAssembly, string text)
        {
            var type = vsEditorAssembly.GetType("Microsoft.VisualStudio.Text.Implementation.StringRebuilder");
            var createMethod = type.GetMethod(
                "Create",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new Type[] { typeof(string) },
                null);
            return createMethod.Invoke(null, new[] { text }); //string text
        }

        private static object CreateGuardedOperations(Assembly vsEditorAssembly)
        {
            var type = vsEditorAssembly.GetType("Microsoft.VisualStudio.Text.Utilities.GuardedOperations");
            var ctor = type.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public,
                null,
                Array.Empty<Type>(),
                null);
            return ctor.Invoke(Array.Empty<object>());
        }

        private static ITextBuffer CreateTextBuffer(Assembly vsEditorAssembly, IContentType contentType, object stringRebuilder, object textDifferencingService, object guardedOperations)
        {
            var type = vsEditorAssembly.GetType("Microsoft.VisualStudio.Text.Implementation.TextBuffer");
            var ctor = type.GetConstructors().Single(c => c.GetParameters().Length == 4);
            return (ITextBuffer)ctor.Invoke(new[] { contentType, stringRebuilder, textDifferencingService, guardedOperations });
        }

        private static int GcCount() => Enumerable.Range(0, GC.MaxGeneration - 1).Select(gen => GC.CollectionCount(gen)).Sum();
    }
}