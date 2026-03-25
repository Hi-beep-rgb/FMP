using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace CodyDreams.Solutions.TerraCanvas
{

// This class is a private helper and not a MonoBehaviour, so it won't appear in the editor.
    internal static class TerraCanvasLiteReviewManager
    {
        private const string FILE_NAME = "ReviewData.json";
        private static readonly string _filePath;

        [System.Serializable]
        internal class AssetReviewData
        {
            public string AssetID;
            public int UsageCount;
            public string LastRequestedDate;
            public bool HasConsideredReview;
            public bool HasReviewed;

            public float AssetPrice;
            public string AssetVersion;
        }

        [System.Serializable]
        private class AllAssetData
        {
            public List<AssetReviewData> assets = new List<AssetReviewData>();
        }

        private static AllAssetData _data;

        // Unique ID for the current asset. This is a constant as requested.
        private const string CurrentAssetID = "Terra Canvas Lite";

        private const string FirstTimeMessage =
            "Thank you for considering \"{0}\" asset. We love to see that and it is big for us your kind review." +
            " ,Your review helps us push updates faster";

        private const string ReturningUserMessage =
            "Thank you for returning for us again, we will try our best to give to you a good product. Mind you leaving a review?";

        private const string LoyalUserMessage = "Thank you for your loyalty! Mind us helping to reach more people?";

        private const string LongTimeMessage =
            "Hey! We’ve noticed you’ve been using Terra Canvas Lite for a long time and exploring its features. If you have" +
            " a minute, we’d love your thoughts — your feedback helps us improve the experience for you and other developers.";

        private const string AssetUrl = "https://assetstore.unity.com/publishers/95756?";

        private const int UsageThersold = 30;

        static TerraCanvasLiteReviewManager()
        {
            _filePath = Path.Combine(Application.persistentDataPath, FILE_NAME);
            LoadData();
        }

        // This is the public method you'll call from your tiny bootstrapper script.
        [InitializeOnLoadMethod]
        private static void OnEditorStart()
        {
            OnAssetUsed(CurrentAssetID);
        }

        [MenuItem("Window/Cody Dremas/FeedBack windows/Terra Canvas Lite")]
        public static void RunFromMenu()
        {
            OnAssetUsed(CurrentAssetID, true);
            ;
        }


        private static void OnAssetUsed(string assetId, bool voluntary = false)
        {
            var currentAsset = _data.assets.Find(x => x.AssetID == assetId);
            if (currentAsset == null)
            {
                currentAsset = new AssetReviewData { AssetID = assetId };
                _data.assets.Add(currentAsset);
            }

            if (!voluntary)
                currentAsset.UsageCount++;

            if (CanShowPrompt(currentAsset) || voluntary)
            {
                ShowReviewPrompt(currentAsset);
            }

            SaveData();
        }

        private static bool CanShowPrompt(AssetReviewData data)
        {
            if (data.HasReviewed)
            {
                return false;
            }

            // Only show if the asset has been used a certain number of times.
            if (data.UsageCount < UsageThersold)
            {
                return false;
            }

            // Don't show again too quickly if they haven't reviewed yet.
            if (data.HasConsideredReview)
            {
                if (!string.IsNullOrEmpty(data.LastRequestedDate))
                {
                    var lastRequest = DateTime.Parse(data.LastRequestedDate);
                    if ((DateTime.Now - lastRequest).TotalDays < 7) return false;
                }
            }

            return true;
        }

        private static void ShowReviewPrompt(AssetReviewData data)
        {
            string message = GetPersonalizedMessage(data);

            // This is the pop-up window with buttons.
            int UserChoice = EditorUtility.DisplayDialogComplex(
                "A quick question!",
                message,
                "Yes, I'd like to help!",
                "No, maybe later",
                "Never show this again"
            );

            data.LastRequestedDate = DateTime.Now.ToString();

            if (UserChoice == 0)
            {
                Application.OpenURL(AssetUrl);
                ShowReviewConfirmation(data);
                data.HasConsideredReview = true;
            }
            else if (UserChoice == 2)
            {
                data.HasReviewed = true;
            }
        }

        private static void ShowReviewConfirmation(AssetReviewData data)
        {
            // This is the second pop-up window
            bool confirmedReview = EditorUtility.DisplayDialog(
                "Thank you!",
                "Looks like you left a review. Thank you for your support!",
                "I already reviewed",
                "I'll review later"
            );

            if (confirmedReview)
            {
                data.HasReviewed = true;
            }
            else
            {
                // Reset the 'HasConsideredReview' flag to show the pop-up again later.
                data.HasConsideredReview = false;
            }
        }

        private static string GetPersonalizedMessage(AssetReviewData data)
        {
            int totalAssets = _data.assets.Count;

            if (data.UsageCount >= 1000)
                return string.Format(LongTimeMessage, data.AssetID);
            if (totalAssets == 1)
            {
                return string.Format(FirstTimeMessage, data.AssetID);
            }

            if (totalAssets == 2)
            {
                return ReturningUserMessage;
            }

            return LoyalUserMessage;
        }

        // --- Data Management Methods (as discussed previously) ---
        private static void LoadData()
        {
            if (File.Exists(_filePath))
            {
                try
                {
                    string json = File.ReadAllText(_filePath);
                    _data = JsonUtility.FromJson<AllAssetData>(json) ?? new AllAssetData();
                }
                catch
                {
                    _data = new AllAssetData();
                }
            }
            else
            {
                _data = new AllAssetData();
            }
        }


        private static void SaveData()
        {
            string json = JsonUtility.ToJson(_data, true); // Set to 'true' for pretty printing
            File.WriteAllText(_filePath, json);
        }
    }
}