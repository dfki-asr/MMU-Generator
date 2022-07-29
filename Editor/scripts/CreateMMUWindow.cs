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


        if (mmuCreation == null)
        {
            //This probably happend due to a script reload, so try to load stored MMUCreation progress from session storage
            if (!CreationStorage.TryLoadCurrent(CreationStorage.Location.Session, out this.mmuCreation))
            {
                if(GUILayout.Button("Reset not Loaded"))
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
            if (this.mmuCreation.Description.Name != null)
                if (this.mmuCreation.Description.Name == "")
                    notcomplete = true;
                else
                    notcomplete = false;
            else
                notcomplete = true;

            EditorGUI.BeginDisabledGroup(notcomplete);
            if (GUILayout.Button("Setup"))
            {
                MMUFactory.Setup(mmuCreation);
            }
            EditorGUI.EndDisabledGroup();
            if (notcomplete)
                EditorGUILayout.HelpBox("Please fill out the required fields.", MessageType.Warning);
        }
        else if (mmuCreation.Status == MMUCreation.CreationStatus.Completed)
        {
            this.mmuCreation.Description.Version = EditorGUILayout.TextField("Version", this.mmuCreation.Description.Version);
            if (GUILayout.Button("Export MMU-Project to Folder"))
            {
                //string selectedFilePath = EditorUtility.SaveFilePanel("Export zip file destination", "", "", "zip");
                string hint = Path.GetDirectoryName(Path.GetDirectoryName(Application.dataPath));
                string selectedFilePath = EditorUtility.SaveFolderPanel("Export zip file destination", hint, "");
                if (!string.IsNullOrEmpty(selectedFilePath))
                {
                    MMUFactory.Export(mmuCreation, selectedFilePath);
                    EditorUtility.DisplayDialog("MMU exported", $"The MMU {mmuCreation.Description.Name} has been exported", "OK");
                }
            }

            if (GUILayout.Button("Create a new MMU"))
            {
                bool createNew = EditorUtility.DisplayDialog("Confirm creation of new MMU",
                    "When creating a new MMU the current one will be deleted from the project. Please use the export functionality if this may be necessary.",
                    "Delete", "Cancel");
                if (createNew)
                {
                    this.mmuCreation.Dispose();
                    this.mmuCreation = MMUFactory.New();
                }
                else
                {
                    EditorUtility.DisplayDialog("Canceled", "Creation of new MMU has been canceled. Therefore, the current MMU has not been deleted.", "OK");
                }
            }
        }
        else if (mmuCreation.Status == MMUCreation.CreationStatus.MissingBehavior)
        {
            EditorGUILayout.LabelField("Please add behavior manually to proceed");
            if(GUILayout.Button("Confirm"))
            {
                if(MMUFactory.SetupPrefabs())
                {
                    CreationStorage.TryLoadCurrent(CreationStorage.Location.Session, out this.mmuCreation);
                }
                return;
            }
            return;
        } else
        {
            if (GUILayout.Button("Reset"))
            {
                this.mmuCreation.Dispose();
                this.mmuCreation = MMUFactory.New();
            }
            return;
        } 
    }

    private void Awake()
    {
        Debug.Log("CreateMMUWindow awake");
        if (!CreationStorage.TryLoadCurrent(CreationStorage.Location.Session, out this.mmuCreation))
        {
            this.mmuCreation = MMUFactory.New();
            EditorApplication.quitting += OnEditorQuitting;
        }
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
        if (CreationStorage.TryLoadCurrent(CreationStorage.Location.Session, out MMUCreation mmuCreation))
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
                    CreationStorage.SaveCurrent(mmuCreation, CreationStorage.Location.Disk);
                }
            }
        }
    }
}
#endif