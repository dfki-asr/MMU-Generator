#if UNITY_EDITOR
using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;

public class CreateMMUWindow : EditorWindow
{
    private MMUCreation mmuCreation;
    private const string defaultScenePath = "Assets/default.unity";

    private static int index;
    private void OnGUI()
    {
        /*

if (PlayerSettings.GetApiCompatibilityLevel(BuildTargetGroup.Standalone) != ApiCompatibilityLevel.NET_Standard_2_0)
{
    if (GUILayout.Button("Set API compatibility level to .NETSTANDARD 2.0"))
    {
        PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Standalone, ApiCompatibilityLevel.NET_Standard_2_0);
        Utils.TriggerScriptReload();
    }
    return;
}

        /*
    if (EditorSceneManager.GetActiveScene() != EditorSceneManager.GetSceneByPath(defaultScenePath)) {
        if (GUILayout.Button("Open default scene"))
        {
            EditorSceneManager.OpenScene(defaultScenePath, OpenSceneMode.Single);
        }            
        return;
    }        */

        var paths = CreationStorage.FindSaveFiles();
        if (mmuCreation == null)
        {
            //This probably happend due to a script reload, so try to load stored MMUCreation progress from session storage
            string[] names = new string[paths.Count];
            for (int i = 0; i < paths.Count; i++)
            {
                names[i] = paths[i].Substring(paths[i].IndexOf("MMUs") + 5, paths[i].IndexOf("Savefiles") - 1 - (paths[i].IndexOf("MMUs") + 5));
            }               
            if (paths.Count > 0)
            {
                EditorGUILayout.LabelField("Press select to choose the currently selected MMU.");
                index = EditorGUILayout.Popup(index, names);
                if (GUILayout.Button("Select MMU"))
                {
                        CreationStorage.TryLoadCurrent(paths[index], out this.mmuCreation);
                        return;
                }
                if (!CreationStorage.TryLoadCurrent(paths[index], out this.mmuCreation))
                
                    Debug.Log("Could not load MMU");
                    if (GUILayout.Button("Reset not Loaded"))
                    {
                        this.mmuCreation = MMUFactory.New();
                    }
                    return;
                
                
        } else
            {
                Debug.Log("No MMU to load");
                if (GUILayout.Button("Reset not Loaded"))
                {
                    this.mmuCreation = MMUFactory.New();
                }
                return;
            }
        }
        if (mmuCreation.Status == MMUCreation.CreationStatus.Created)
        {
            this.mmuCreation.Description.Name = EditorGUILayout.TextField("Name*", this.mmuCreation.Description.Name);
            this.mmuCreation.Description.MotionType = EditorGUILayout.TextField("MotionType", this.mmuCreation.Description.MotionType);
            this.mmuCreation.Description.Author = EditorGUILayout.TextField("Author", this.mmuCreation.Description.Author);
            this.mmuCreation.Description.ShortDescription = EditorGUILayout.TextField("ShortDescription", this.mmuCreation.Description.ShortDescription);
            this.mmuCreation.Description.LongDescription = EditorGUILayout.TextField("LongDescription", this.mmuCreation.Description.LongDescription);
            this.mmuCreation.Description.Version = EditorGUILayout.TextField("Version", this.mmuCreation.Description.Version);

            this.mmuCreation.IsMoCapMMU = EditorGUILayout.BeginToggleGroup("Should the MMU play MoCap recordings?", this.mmuCreation.IsMoCapMMU);
            EditorGUILayout.LabelField($"FBX file path: {this.mmuCreation.FbxFilePath}");
            if (GUILayout.Button("Choose FBX file"))
            {
                string selectedFilePath = EditorUtility.OpenFilePanelWithFilters("Select FBX file", "", new string[] { "FBX file", "fbx" });
                if (!string.IsNullOrEmpty(selectedFilePath))
                {
                    this.mmuCreation.FbxFilePath = selectedFilePath;
                }
            }
            EditorGUILayout.EndToggleGroup();
            bool notcomplete = false;
            if (string.IsNullOrEmpty(this.mmuCreation.Description.Name))
                notcomplete = true;

            EditorGUI.BeginDisabledGroup(notcomplete);
            if (GUILayout.Button("Setup"))
            {
                MMUFactory.Setup(mmuCreation);
                paths = CreationStorage.FindSaveFiles();
                string[] names = new string[paths.Count];
                for (int i = 0; i < paths.Count; i++)
                {
                    names[i] = paths[i].Substring(paths[i].IndexOf("MMUs") + 5, paths[i].IndexOf("Savefiles") - 1 - (paths[i].IndexOf("MMUs") + 5));
                }

                for(int i = 0; i < names.Length; i++)
                {
                    if (names[i] == mmuCreation.Description.Name)
                        index = i; 
                }

                return;
            }
            EditorGUI.EndDisabledGroup();
            if (notcomplete)
                EditorGUILayout.HelpBox("Please fill out the required fields.", MessageType.Warning);
        }
        else if (this.mmuCreation.Status == MMUCreation.CreationStatus.Completed)
        {
            this.mmuCreation.Description.Version = EditorGUILayout.TextField("Version", this.mmuCreation.Description.Version);
            if (GUILayout.Button("Export MMU-Project to Folder"))
            {
                //string selectedFilePath = EditorUtility.SaveFilePanel("Export zip file destination", "", "", "zip");
                string hint = Path.GetDirectoryName(Path.GetDirectoryName(Application.dataPath));
                string selectedFilePath = EditorUtility.SaveFolderPanel("Export zip file destination", hint, "");
                if (!string.IsNullOrEmpty(selectedFilePath))
                {
                    MMUFactory.Export(this.mmuCreation, selectedFilePath);
                    EditorUtility.DisplayDialog("MMU exported", $"The MMU {mmuCreation.Description.Name} has been exported", "OK");
                }
            }

            if (GUILayout.Button("Create a new MMU"))
            {
                CreationStorage.SaveCurrent(this.mmuCreation);
                this.mmuCreation = MMUFactory.New();
                return;
            }
        }
        else if (mmuCreation.Status == MMUCreation.CreationStatus.MissingBehavior)
        {
            EditorGUILayout.LabelField("Please add behavior manually to proceed.");
            if(GUILayout.Button("Confirm"))
            {
                if(MMUFactory.SetupPrefabs())
                {
                    CreationStorage.TryLoadCurrent(paths[index], out this.mmuCreation);
                }
                return;
            }

            string[] names = new string[paths.Count];
            for (int i = 0; i < paths.Count; i++)
            {
                names[i] = paths[i].Substring(paths[i].IndexOf("MMUs") + 5, paths[i].IndexOf("Savefiles") - 1 - (paths[i].IndexOf("MMUs") + 5));
            }
            GUILayout.FlexibleSpace();
            if (paths.Count > 0)
            {
                EditorGUILayout.LabelField("Press select to choose the currently selected MMU.");
                index = EditorGUILayout.Popup(index, names);

                if (GUILayout.Button("Select MMU"))
                {
                    CreationStorage.SaveCurrent(this.mmuCreation);
                    CreationStorage.TryLoadCurrent(paths[index], out this.mmuCreation);
                }
            }
            
            GUILayout.Space(10);
            if (GUILayout.Button("New"))
            {
                CreationStorage.SaveCurrent(this.mmuCreation);
                this.mmuCreation = MMUFactory.New();
            }
            return;
        } else
        {
            string[] names = new string[paths.Count];
            for (int i = 0; i < paths.Count; i++ )
            {
                names[i] = paths[i].Substring(paths[i].IndexOf("MMUs") + 5, paths[i].IndexOf("Savefiles") -1 - (paths[i].IndexOf("MMUs") +5));
            }
            if (paths.Count > 0)
            {
                EditorGUILayout.LabelField("Press select to choose the currently selected MMU.");
                index = EditorGUILayout.Popup(index, names);

                if (GUILayout.Button("Select MMU"))
                {
                    CreationStorage.SaveCurrent(mmuCreation);
                    CreationStorage.TryLoadCurrent(paths[index], out this.mmuCreation);
                }
            }
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("New"))
            {
                CreationStorage.SaveCurrent(this.mmuCreation);
                this.mmuCreation = MMUFactory.New();
            }

            GUILayout.Space(5);
            if (GUILayout.Button("Reset"))
            {
                this.mmuCreation.Dispose();
                this.mmuCreation = MMUFactory.New();
            }
            EditorGUILayout.HelpBox("This will delete the progress of the currently selected MMU.", MessageType.Warning);
            return;
        } 
    }

    private void Awake()
    {
        Debug.Log("CreateMMUWindow awake");
        var paths = CreationStorage.FindSaveFiles();
        if(paths.Count > 0)
        {
            if(!CreationStorage.TryLoadCurrent(paths[index], out this.mmuCreation))
            {
                this.mmuCreation = MMUFactory.New();
                EditorApplication.quitting += OnEditorQuitting;
            }
        } else
        {
            this.mmuCreation = MMUFactory.New();
            EditorApplication.quitting += OnEditorQuitting;
        }
        /*if (!CreationStorage.TryLoadCurrent("//Assets", out this.mmuCreation))
        {
            this.mmuCreation = MMUFactory.New();
            EditorApplication.quitting += OnEditorQuitting;
        }*/
    }

    public static string getPath()
    {
        var paths = CreationStorage.FindSaveFiles();
        if (paths.Count > 0 && index < paths.Count)
        {
            return paths[index];
        }
        return "Assets//";
    }

    [MenuItem("MMI/MMU Creator", false, 0)]
    static void SetupMMU()
    {
        EditorWindow window = GetWindow(typeof(CreateMMUWindow), true, "Setup MMU", true);
        window.ShowUtility();
    }

    [DidReloadScripts]
    private static void OnScriptsReload()
    {
        EditorApplication.quitting += OnEditorQuitting;
    }

    private static void OnEditorQuitting()
    {
        //if (MMUCreation.TryLoad(MMUCreation.CURRENT_CREATION_NAME, false, out MMUCreation mmuCreation))
        var paths = CreationStorage.FindSaveFiles();
        if (paths.Count > 0)
            //TODO: Get the name of the MMU which is selected
        {
            if (CreationStorage.TryLoadCurrent(paths[0], out MMUCreation mmuCreation))
            {
                Debug.Log("Should be loaded correctly.");
                // Check if the mmu has just been created
                // Saving to disk is not necessary in this case as no files have been created yet
                if (mmuCreation.Status != MMUCreation.CreationStatus.Created)
                {
                    bool persistToDisk = EditorUtility.DisplayDialog("Save MMU creation progress?",
                        $"You are currently editing { mmuCreation.Description.Name}. Do you want to save the current progress?",
                        "Yes", "No");
                    if (persistToDisk)
                    {
                        CreationStorage.SaveCurrent(mmuCreation);
                    }
                }
            }
        }

        /*
        if (CreationStorage.TryLoadCurrent("Assets//", out MMUCreation mmuCreation))
        {
            // Check if the mmu has just been created
            // Saving to disk is not necessary in this case as no files have been created yet
            if (mmuCreation.Status != MMUCreation.CreationStatus.Created)
            {
                bool persistToDisk = EditorUtility.DisplayDialog("Save MMU creation progress?",
                    $"You are currently editing { mmuCreation.Description.Name}. Do you want to save the current progress?",
                    "Yes", "No");
                if (persistToDisk)
                {
                    CreationStorage.SaveCurrent(mmuCreation);
                }
            }
        }*/
    }
}
#endif