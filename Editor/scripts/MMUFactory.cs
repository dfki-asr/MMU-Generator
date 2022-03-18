﻿#if UNITY_EDITOR
using MMICSharp.Common.Communication;
using MMIStandard;
using MMIUnity;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Callbacks;
using UnityEngine;

/// <summary>
/// This class provides functionality of intermediate steps in the process of a MMU creation.
/// </summary>
public class MMUFactory
{

    public static string packageName = "de.dfki.mmu-generator";
    /// <summary>
    /// Creates a new MMU. 
    /// </summary>
    /// <returns>The creation progress object for thew new MMU</returns>
    public static MMUCreation New()
    {
        AssetImportHelper.PendingMotionImports.Clear();
        CreationStorage.DeleteCurrent();
        var newCreation = new MMUCreation();
        newCreation.Description.Version = "1.0";
        newCreation.Description.MotionType = "";
        CreationStorage.SaveCurrent(newCreation, CreationStorage.Location.Session);
        return newCreation;
    }

    /// <summary>
    /// Creates the file structure for the given MMU creation.
    /// </summary>
    /// <param name="mmuCreation">The MMU creation for which the file structure should be created</param>
    /// <returns>The creation object</returns>
    public static MMUCreation Setup(MMUCreation mmuCreation)
    {
        SetupFileStructure(mmuCreation);
        return mmuCreation;
    }

    /// <summary>
    /// Exports the given MMU creation as zip file to the specified path.
    /// </summary>
    /// <param name="mmuCreation">The MMU creation that should be exported</param>
    /// <param name="zipFilePath">Complete file path (including file name) where the zip file should be created</param>
    /// <returns>The creation object</returns>
    public static MMUCreation Export(MMUCreation mmuCreation, string zipFilePath)
    {
        //Create a temp directory
        string tempDirectory = $"TempAutoGenerated/{mmuCreation.Description.Name}/";
        if (Directory.Exists(tempDirectory))
            Directory.Delete(tempDirectory, true);

        Directory.CreateDirectory(tempDirectory);


        //Change the name of the bundle
        MMUGenerator.ChangeBundleName($"{mmuCreation.Description.Name}assets", mmuCreation.Prefab);

        //Build the asset bundle and place in autogenerated
        BuildPipeline.BuildAssetBundles(tempDirectory, BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows);

        //Find all specific class files
        List<string> classFiles = MMUGenerator.GetFiles("Assets//" + mmuCreation.Description.Name + "//Scripts//").Where(s => Path.GetExtension(s) == ".cs").ToList();

        //Add  all common class files
        classFiles.AddRange(MMUGenerator.GetFiles("Assets//MMUGenerator//CommonScripts").Where(s => Path.GetExtension(s) == ".cs").ToList());


        //Add the required dependencies for a basic MMU
        List<string> dllFiles = MMUGenerator.GetFiles("Assets//MMUGenerator//Dependencies").Where(s => Path.GetExtension(s) == ".dll").ToList();

        //Add the specific dependencies for the MMU (if defined)
        dllFiles.AddRange(MMUGenerator.GetFiles("Assets//" + mmuCreation.Description.Name + "//Dependencies").Where(s => Path.GetExtension(s) == ".dll").ToList());

        //Generate the dll based on the given class and dll files
        if (!MMUGenerator.GenerateDll(classFiles, dllFiles, tempDirectory, mmuCreation.Description.Name))
        {
            EditorUtility.DisplayDialog("Error at generating dll.", "Dll cannot be compiled. Please check the error messages and ensure that all required dependencies (despite the ones in MMUGenerator/Dependencies) are in the dependencies folder of the MMU. " +
                "Moreover please ensure that all references source code cs files are either in the Scripts folder of the MMU or in the CommonSourceFolder of the MMUGenerator.", "Continue");
            return mmuCreation;
        }

        //Generate the description file
        MMUGenerator.GenerateDescription(mmuCreation.Description, tempDirectory);

        //Cleanup the directory
        MMUGenerator.CleanUpDirectory(tempDirectory, mmuCreation.Description.Name);

        if (File.Exists(zipFilePath))
        {
            File.Delete(zipFilePath);
        }
        ZipFile.CreateFromDirectory(tempDirectory, zipFilePath, System.IO.Compression.CompressionLevel.Fastest, true);

        //Remove all temporary generated files
        string[] tempFiles = Directory.GetFiles(tempDirectory);
        for (int i = 0; i < tempFiles.Length; i++)
        {
            File.Delete(tempFiles[i]);
        }

        //Remove the temp directory
        Directory.Delete(tempDirectory);

        EditorUtility.RevealInFinder(zipFilePath);

        Debug.Log("MMU successfully generated!");

        CreationStorage.SaveCurrent(mmuCreation, CreationStorage.Location.Session);

        return mmuCreation;
    }

    private static void SetupFileStructure(MMUCreation mmuCreation)
    {
        var description = mmuCreation.Description;
        Directory.CreateDirectory("Assets//" + description.Name);
        Directory.CreateDirectory("Assets//" + description.Name + "//Dependencies");
        Directory.CreateDirectory("Assets//" + description.Name + "//Scripts");

        //Create a unique id
        description.ID = System.Guid.NewGuid().ToString();
        description.Language = "UnityC#";
        description.Dependencies = new List<MDependency>();
        description.AssemblyName = description.Name + ".dll";

        //Generate the .cs file
        //Get the template for the auto-generated MMU class
        string mmuTemplate;

        if (mmuCreation.IsMoCapMMU)
        {
            mmuTemplate = File.ReadAllText($"Packages/{packageName}/Editor/resources/MMUTemplateBaseClassAnimator.template");
        }
        else
        {
            mmuTemplate = File.ReadAllText($"Packages/{packageName}/Editor/resources/MMUTemplateBaseClass.template");
        }

        //Replace the placeholders
        mmuTemplate = mmuTemplate.Replace("CLASS_NAME", description.Name);
        mmuTemplate = mmuTemplate.Replace("MOTION_TYPE", description.MotionType);

        //Write the class to the location
        File.WriteAllText("Assets//" + description.Name + "//Scripts//" + description.Name + ".cs", mmuTemplate);

        //Store the description
        File.WriteAllText("Assets//" + description.Name + "//description.json", Serialization.ToJsonString(description));

        Debug.Log("File structure for MMU " + description.Name + " successfully created!");

        var tpose = Resources.Load("tpose");
        var instance = GameObject.Instantiate(tpose) as GameObject;
        instance.name = description.Name;

        mmuCreation.Instance = instance;

        if (mmuCreation.IsMoCapMMU)
        {
            var animatorController = AnimatorController.CreateAnimatorControllerAtPath($"Assets/{description.Name}/{description.Name}.controller");
            animatorController.AddParameter("AnimationDone", AnimatorControllerParameterType.Bool);
            var firstLayer = animatorController.layers[0]; //first layer is lost when storing as asset -> saving for later

            var animator = instance.GetComponent<Animator>();
            animator.runtimeAnimatorController = animatorController;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            mmuCreation.AnimatorController = animatorController;

            string fbxAssetPath = $@"Assets/{description.Name}/{Path.GetFileName(mmuCreation.FbxFilePath)}";
            File.Copy(mmuCreation.FbxFilePath, fbxAssetPath);

            AssetImportHelper.PendingMotionImports.Add(fbxAssetPath, () =>
            {
                Debug.Log("PendingMotionCallback");
                
                var animationClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(fbxAssetPath);

                if (animatorController.layers.Length == 0)
                {
                    animatorController.AddLayer(firstLayer);
                }
                var mainState = animatorController.AddMotion(animationClip);
                mainState.AddStateMachineBehaviour<AnimationEndEvent>();

                var secondState = animatorController.AddMotion(animationClip);

                var loopTransition = mainState.AddExitTransition();
                loopTransition.destinationState = secondState;
                loopTransition.exitTime = 1;
                loopTransition.hasExitTime = true;
                loopTransition.duration = 0;
                loopTransition.offset = 0.99f;

                var backwardTransition = secondState.AddExitTransition();
                backwardTransition.destinationState = mainState;
                backwardTransition.exitTime = 1;
                backwardTransition.hasExitTime = true;
                backwardTransition.duration = 0;
                backwardTransition.offset = 0;

                mmuCreation.Status = MMUCreation.CreationStatus.AnimationSetup;
                CreationStorage.SaveCurrent(mmuCreation, CreationStorage.Location.Session);

            });
        }

        mmuCreation.Status = MMUCreation.CreationStatus.FilesSetup;
        CreationStorage.SaveCurrent(mmuCreation, CreationStorage.Location.Session);

        //Refresh the asset database to show the new filestructure
        AssetDatabase.Refresh();
    }

    public static bool SetupPrefabs()
    {
        if (CreationStorage.TryLoadCurrent(CreationStorage.Location.Session, out MMUCreation mmuCreation))
        {
            var description = mmuCreation.Description;
            var instance = mmuCreation.Instance;
            Component c = null;
            c = instance.GetComponent<UnityMMUBase>();

            if(c != null)
            {
                //Do the initialization
                AutoCodeGenerator.SetupBoneMapping(instance);
                AutoCodeGenerator.AutoGenerateScriptInitialization(instance);

                //Assign the game joint prefab
                instance.GetComponent<UnityAvatarBase>().gameJointPrefab = Resources.Load("singleBone") as GameObject;

                //Create prefab
                bool success = false;
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, $"Assets/{description.Name}/{description.Name}.prefab", out success);
                Debug.Log("Creating prefab: " + success);

                mmuCreation.Prefab = prefab;

                mmuCreation.Status = MMUCreation.CreationStatus.Completed;
                CreationStorage.SaveCurrent(mmuCreation, CreationStorage.Location.Session);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Helper function that handles Unity Editor Script reloads.
    /// Script reloads are necessary when new data types should be recognized but
    /// lead to destruction of all (even static) variables and references.
    /// </summary>
    [DidReloadScripts(1000)]
    private static void OnScriptsReload()
    {
        Debug.Log("ScriptReloadCallback");
        if (CreationStorage.TryLoadCurrent(CreationStorage.Location.Session, out MMUCreation mmuCreation))
        {
            if (mmuCreation.Status == MMUCreation.CreationStatus.FilesSetup && mmuCreation.IsMoCapMMU)
            {
                Debug.Log("Motion setup after script reload");

                string fbxAssetPath = $@"Assets/{mmuCreation.Description.Name}/{Path.GetFileName(mmuCreation.FbxFilePath)}";
                var animationClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(fbxAssetPath);

                var newState = mmuCreation.AnimatorController.AddMotion(animationClip);
                newState.AddStateMachineBehaviour<AnimationEndEvent>();

                var loopTransition = newState.AddExitTransition();
                loopTransition.destinationState = newState;
                loopTransition.exitTime = 1;
                loopTransition.hasExitTime = true;
                loopTransition.duration = 0;

                mmuCreation.Status = MMUCreation.CreationStatus.AnimationSetup;
                CreationStorage.SaveCurrent(mmuCreation, CreationStorage.Location.Session);
            }

            if (mmuCreation.IsMoCapMMU && mmuCreation.Status == MMUCreation.CreationStatus.AnimationSetup
                || !mmuCreation.IsMoCapMMU && mmuCreation.Status == MMUCreation.CreationStatus.FilesSetup)
            {
                var description = mmuCreation.Description;
                var instance = mmuCreation.Instance;
                Component component = null;
                //Add the script directly to the object

                System.Type compType = System.Type.GetType(description.Name);
                Debug.Log($"Type: {compType}");
                component = instance.AddComponent(compType); //ToDo: this type exists only after asset import
                                                             //Fix: https://docs.unity3d.com/ScriptReference/Callbacks.DidReloadScripts.html
                if (component == null)
                {
                    Debug.Log("Still waiting for scripts to reload");
                    mmuCreation.Status = MMUCreation.CreationStatus.MissingBehavior;
                    CreationStorage.SaveCurrent(mmuCreation, CreationStorage.Location.Session);
                }

                SetupPrefabs();
            }
        }
    }
}
#endif