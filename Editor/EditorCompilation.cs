using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using System.Linq;

[InitializeOnLoad]
public class EditorCompilation
{
    public static bool isCompilerErr = false;

    static EditorCompilation()
    {
        isCompilerErr = false;
        if (Application.isBatchMode)
            CompilationPipeline.assemblyCompilationFinished += ProcessBatchModeCompileFinish;
    }

    private static void ProcessBatchModeCompileFinish(string s, CompilerMessage[] compilerMessages)
    {
        if (compilerMessages.Count(m => m.type == CompilerMessageType.Error) > 0)
        {
            isCompilerErr = true;
        }
    }
}