using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class Utils
{
    public static void TriggerScriptReload()
    {
#if UNITY_2019_3_OR_NEWER
        UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
#elif UNITY_2017_1_OR_NEWER
        var editorAssembly = Assembly.GetAssembly(typeof(Editor));
        var editorCompilationInterfaceType = editorAssembly.GetType("UnityEditor.Scripting.ScriptCompilation.EditorCompilationInterface");
        var dirtyAllScriptsMethod = editorCompilationInterfaceType.GetMethod("DirtyAllScripts", BindingFlags.Static | BindingFlags.Public);
        dirtyAllScriptsMethod.Invoke(editorCompilationInterfaceType, null);
#endif
    }

}
