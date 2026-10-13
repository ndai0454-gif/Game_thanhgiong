using UnityEngine;
using UnityEditor;
using System;
using System.IO;

namespace ThanhGiong.EditorTools
{
    /// <summary>
    /// AssetPostprocessor that automatically configures FBX and GLB animations
    /// exported from ActionForge (https://actionforge.app) into Assets/Animations/ActionForge.
    /// </summary>
    public class ActionForgePostprocessor : AssetPostprocessor
    {
        private const string ActionForgeFolder = "Assets/Animations/ActionForge/";

        internal static bool IsActionForgeModel(string path)
        {
            return path.StartsWith(ActionForgeFolder, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(Path.GetExtension(path), ".fbx", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsLoopClip(string name)
        {
            string n = name.ToLowerInvariant();
            return n.Contains("idle") || n.Contains("walk") || n.Contains("run") ||
                   n.Contains("gallop") || n.Contains("loop") || n.Contains("riding");
        }

        internal static void ConfigureClips(ModelImporter importer, string path)
        {
            var source = importer.clipAnimations;
            if (source == null || source.Length == 0) source = importer.defaultClipAnimations;
            if (source == null || source.Length == 0) return;
            foreach (var clip in source)
            {
                clip.loopTime = IsLoopClip(clip.name) || IsLoopClip(Path.GetFileNameWithoutExtension(path));
                clip.loopPose = clip.loopTime;
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
            }
            importer.clipAnimations = source;
        }

        private void OnPreprocessAnimation()
        {
            if (!IsActionForgeModel(assetPath)) return;

            ModelImporter modelImporter = assetImporter as ModelImporter;
            if (modelImporter == null) return;

            ConfigureClips(modelImporter, assetPath);
        }

        private void OnPreprocessModel()
        {
            if (!IsActionForgeModel(assetPath)) return;

            ModelImporter modelImporter = assetImporter as ModelImporter;
            if (modelImporter == null) return;

            modelImporter.animationType = ModelImporterAnimationType.Generic;
        }
    }
}
