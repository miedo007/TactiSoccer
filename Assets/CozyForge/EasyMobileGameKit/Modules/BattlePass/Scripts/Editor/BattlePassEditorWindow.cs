using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using CozyFramework;

namespace CozyFramework.Editor
{
    public class BattlePassEditorWindow : EditorWindow
    {
        private string jsonSource = "";
        private BattlePassView.BattlePassConfig battlePassConfig;

        private List<bool> tierFoldouts = new List<bool>();
        private List<List<bool>> freeRewardFoldouts = new List<List<bool>>();
        private List<List<bool>> premiumRewardFoldouts = new List<List<bool>>();

        private Vector2 scrollPos;

        private int startYear = 2025, startMonth = 1, startDay = 1, startHour = 0, startMinute = 0;
        private int endYear = 2025, endMonth = 1, endDay = 1, endHour = 0, endMinute = 0;

        private readonly string[] rewardTypeOptions = { "Currency", "Item" };
        private readonly string[] monthNames = DateTimeFormatInfo.CurrentInfo.MonthNames;
        private readonly string[] hours = GenerateNumberOptions(0, 23);
        private readonly string[] minutes = GenerateNumberOptions(0, 59);

        [MenuItem("Tools/Cozy Forge/Battle Pass Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<BattlePassEditorWindow>();
            window.titleContent = new GUIContent("Battle Pass Editor");
            window.Show();
        }

        private void OnEnable()
        {
            if (battlePassConfig == null)
            {
                battlePassConfig = new BattlePassView.BattlePassConfig();
            }

            // Initialize custom date fields
            if (battlePassConfig.startTime > 0)
            {
                InitializeDateFields(
                    UnixTimeStampToLocalDateTime(battlePassConfig.startTime),
                    ref startYear, ref startMonth, ref startDay, ref startHour, ref startMinute
                );
            }

            if (battlePassConfig.endTime > 0)
            {
                InitializeDateFields(
                    UnixTimeStampToLocalDateTime(battlePassConfig.endTime),
                    ref endYear, ref endMonth, ref endDay, ref endHour, ref endMinute
                );
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("BATTLE PASS Editor", EditorStyles.boldLabel);

            // JSON source
            EditorGUILayout.LabelField("JSON Source:");
            jsonSource = EditorGUILayout.TextArea(jsonSource, GUILayout.Height(60));

            EditorGUILayout.Space();

            // Buttons for Load, Generate, and Copy JSON
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Load JSON", GUILayout.Width(120)))
            {
                LoadJSON();
            }
            if (GUILayout.Button("Generate JSON", GUILayout.Width(120)))
            {
                GenerateJSON();
            }
            if (GUILayout.Button("Copy JSON", GUILayout.Width(120)))
            {
                GUIUtility.systemCopyBuffer = jsonSource;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Battle Pass Configuration
            EditorGUILayout.LabelField("BattlePass Config", EditorStyles.boldLabel);
            string passNumber = EditorGUILayout.TextField("Pass ID (Number)", ExtractPassNumber(battlePassConfig.passId));
            battlePassConfig.passId = $"BATTLE_PASS_{passNumber}";

            battlePassConfig.passName = EditorGUILayout.TextField("Pass Name", battlePassConfig.passName);

            EditorGUILayout.Space();

            // Start and End Date-Time Fields
            EditorGUILayout.LabelField("Start Time (Local Date/Time)");
            CreateCustomDateDropdown(ref startYear, ref startMonth, ref startDay, ref startHour, ref startMinute);
            battlePassConfig.startTime = LocalDateTimeToUnixTimeStamp(
                ValidateDateTime(startYear, startMonth, startDay, startHour, startMinute)
            );

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("End Time (Local Date/Time)");
            CreateCustomDateDropdown(ref endYear, ref endMonth, ref endDay, ref endHour, ref endMinute);
            battlePassConfig.endTime = LocalDateTimeToUnixTimeStamp(
                ValidateDateTime(endYear, endMonth, endDay, endHour, endMinute)
            );

            EditorGUILayout.Space();

            // Add and display tiers
            if (GUILayout.Button("Add Tier", GUILayout.Height(25)))
            {
                AddNewTier();
            }

            EditorGUILayout.Space();
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            for (int i = 0; i < battlePassConfig.tiers.Count; i++)
            {
                EnsureTierFoldoutSize(i);

                EditorGUILayout.BeginVertical("box");
                var tier = battlePassConfig.tiers[i];

                tierFoldouts[i] = EditorGUILayout.Foldout(tierFoldouts[i], $"Tier {i + 1} (XP: {tier.requiredXP})", true);

                if (GUILayout.Button("Remove Tier", GUILayout.Width(100)))
                {
                    RemoveTier(i);
                    EditorGUILayout.EndVertical();
                    break;
                }

                if (tierFoldouts[i])
                {
                    EditorGUI.indentLevel++;
                    tier.requiredXP = EditorGUILayout.IntField("Required XP", tier.requiredXP);

                    DrawRewardList(ref tier.freeRewards, i, freeRewardFoldouts, "Free Rewards", AddFreeReward, RemoveFreeReward);
                    DrawRewardList(ref tier.premiumRewards, i, premiumRewardFoldouts, "Premium Rewards", AddPremiumReward, RemovePremiumReward);

                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();
        }

        #region Helper Methods for Date/Time

        private void InitializeDateFields(DateTime date, ref int year, ref int month, ref int day, ref int hour, ref int minute)
        {
            year = date.Year;
            month = date.Month;
            day = date.Day;
            hour = date.Hour;
            minute = date.Minute;
        }

        private void CreateCustomDateDropdown(ref int year, ref int month, ref int day, ref int hour, ref int minute)
        {
            year = Mathf.Clamp(EditorGUILayout.IntField("Year", year), 1, 9999);
            month = EditorGUILayout.Popup("Month", month - 1, monthNames) + 1;

            string[] dayOptions = GenerateDayOptions(year, month);
            day = EditorGUILayout.Popup($"Day ({monthNames[month - 1]})", day - 1, dayOptions) + 1;

            hour = EditorGUILayout.Popup("Hour", hour, hours);
            minute = EditorGUILayout.Popup("Minute", minute, minutes);
        }

        private string[] GenerateDayOptions(int year, int month)
        {
            int daysInMonth = DateTime.DaysInMonth(year, month);
            string[] options = new string[daysInMonth];
            for (int i = 0; i < daysInMonth; i++)
            {
                options[i] = $"{i + 1}{GetOrdinalSuffix(i + 1)} of {monthNames[month - 1]}";
            }
            return options;
        }

        private static string[] GenerateNumberOptions(int start, int end)
        {
            string[] options = new string[end - start + 1];
            for (int i = start; i <= end; i++)
            {
                options[i - start] = i.ToString("D2");
            }
            return options;
        }

        private static string GetOrdinalSuffix(int number)
        {
            if (number % 100 >= 11 && number % 100 <= 13)
                return "th";

            return (number % 10) switch
            {
                1 => "st",
                2 => "nd",
                3 => "rd",
                _ => "th"
            };
        }


        private DateTime ValidateDateTime(int year, int month, int day, int hour, int minute)
        {
            year = Mathf.Clamp(year, 1, 9999);
            month = Mathf.Clamp(month, 1, 12);
            day = Mathf.Clamp(day, 1, DateTime.DaysInMonth(year, month));
            hour = Mathf.Clamp(hour, 0, 23);
            minute = Mathf.Clamp(minute, 0, 59);

            return new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Local);
        }

        private DateTime UnixTimeStampToLocalDateTime(long unixTimeStamp)
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixTimeStamp).LocalDateTime;
        }

        private long LocalDateTimeToUnixTimeStamp(DateTime localDateTime)
        {
            return new DateTimeOffset(localDateTime.ToUniversalTime()).ToUnixTimeSeconds();
        }

        #endregion

        #region Pass ID Enforcement

        private string ExtractPassNumber(string passId)
        {
            if (string.IsNullOrEmpty(passId) || !passId.StartsWith("BATTLE_PASS_"))
                return "1";

            string[] parts = passId.Split('_');
            return parts.Length > 2 ? parts[2] : "1";
        }

        #endregion

        #region Reward List Management

        private void DrawRewardList(ref List<BattlePassView.RewardData> rewardList, int tierIndex,
            List<List<bool>> foldouts, string label, Action<int> addAction, Action<int, int> removeAction)
        {
            if (rewardList == null) rewardList = new List<BattlePassView.RewardData>();
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button($"Add {label}", GUILayout.Width(150)))
            {
                addAction.Invoke(tierIndex);
            }
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < rewardList.Count; i++)
            {
                EnsureRewardFoldoutSize(foldouts, tierIndex, i);
                var reward = rewardList[i];

                EditorGUILayout.BeginHorizontal();
                foldouts[tierIndex][i] = EditorGUILayout.Foldout(foldouts[tierIndex][i], $"{label} {i + 1}", true);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("X", GUILayout.Width(25)))
                {
                    removeAction.Invoke(tierIndex, i);
                    EditorGUILayout.EndHorizontal();
                    break;
                }
                EditorGUILayout.EndHorizontal();

                if (i < rewardList.Count && foldouts[tierIndex][i])
                {
                    EditorGUI.indentLevel++;
                    reward.type = DrawRewardTypeDropdown(reward.type);
                    reward.id = EditorGUILayout.TextField("ID", reward.id);
                    reward.quantity = EditorGUILayout.IntField("Quantity", reward.quantity);
                    rewardList[i] = reward;
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.Space(4);
            }
        }

        private void AddFreeReward(int tierIndex)
        {
            var tier = battlePassConfig.tiers[tierIndex];
            var reward = new BattlePassView.RewardData { type = "Currency", id = "GOLD", quantity = 1 };
            tier.freeRewards.Add(reward);
            freeRewardFoldouts[tierIndex].Add(true);
        }

        private void RemoveFreeReward(int tierIndex, int rewardIndex)
        {
            var tier = battlePassConfig.tiers[tierIndex];
            tier.freeRewards.RemoveAt(rewardIndex);
            freeRewardFoldouts[tierIndex].RemoveAt(rewardIndex);
        }

        private void AddPremiumReward(int tierIndex)
        {
            var tier = battlePassConfig.tiers[tierIndex];
            var reward = new BattlePassView.RewardData { type = "Currency", id = "GOLD", quantity = 1 };
            tier.premiumRewards.Add(reward);
            premiumRewardFoldouts[tierIndex].Add(true);
        }

        private void RemovePremiumReward(int tierIndex, int rewardIndex)
        {
            var tier = battlePassConfig.tiers[tierIndex];
            tier.premiumRewards.RemoveAt(rewardIndex);
            premiumRewardFoldouts[tierIndex].RemoveAt(rewardIndex);
        }

        private void EnsureRewardFoldoutSize(List<List<bool>> foldouts, int tierIndex, int rewardIndex)
        {
            while (foldouts.Count <= tierIndex)
            {
                foldouts.Add(new List<bool>());
            }
            while (foldouts[tierIndex].Count <= rewardIndex)
            {
                foldouts[tierIndex].Add(true);
            }
        }

        #endregion

        #region Tier Management

        private void AddNewTier()
        {
            var newTier = new BattlePassView.TierData
            {
                requiredXP = 100,
                freeRewards = new List<BattlePassView.RewardData>(),
                premiumRewards = new List<BattlePassView.RewardData>()
            };
            battlePassConfig.tiers.Add(newTier);

            tierFoldouts.Add(true);
            freeRewardFoldouts.Add(new List<bool>());
            premiumRewardFoldouts.Add(new List<bool>());
        }

        private void RemoveTier(int index)
        {
            battlePassConfig.tiers.RemoveAt(index);
            tierFoldouts.RemoveAt(index);
            freeRewardFoldouts.RemoveAt(index);
            premiumRewardFoldouts.RemoveAt(index);
        }

        private void EnsureTierFoldoutSize(int i)
        {
            while (tierFoldouts.Count <= i)
            {
                tierFoldouts.Add(true);
            }
            while (freeRewardFoldouts.Count <= i)
            {
                freeRewardFoldouts.Add(new List<bool>());
            }
            while (premiumRewardFoldouts.Count <= i)
            {
                premiumRewardFoldouts.Add(new List<bool>());
            }
        }

        #endregion

        #region Reward Type Dropdown

        private string DrawRewardTypeDropdown(string currentType)
        {
            int selectedIndex = Array.IndexOf(rewardTypeOptions, currentType);
            if (selectedIndex < 0) selectedIndex = 0;

            int newSelectedIndex = EditorGUILayout.Popup("Reward Type", selectedIndex, rewardTypeOptions);
            return rewardTypeOptions[newSelectedIndex];
        }

        #endregion

        #region Load/Generate JSON

        private void LoadJSON()
        {
            try
            {
                if (!string.IsNullOrEmpty(jsonSource))
                {
                    battlePassConfig = JsonConvert.DeserializeObject<BattlePassView.BattlePassConfig>(jsonSource);
                    RebuildFoldouts();
                    Debug.Log("[BattlePassEditor] Loaded config from JSON.");
                }
                else
                {
                    battlePassConfig = new BattlePassView.BattlePassConfig();
                    Debug.Log("JSON empty, created a new BattlePassConfig.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[BattlePassEditor] Error parsing JSON: " + e);
            }
        }

        private void GenerateJSON()
        {
            try
            {
                jsonSource = JsonConvert.SerializeObject(battlePassConfig, Formatting.Indented);
                Debug.Log("[BattlePassEditor] Generated JSON from config.");
            }
            catch (Exception e)
            {
                Debug.LogError("[BattlePassEditor] Error generating JSON: " + e);
            }
        }

        #endregion

        #region Foldout Management

        private void RebuildFoldouts()
        {
            tierFoldouts.Clear();
            freeRewardFoldouts.Clear();
            premiumRewardFoldouts.Clear();

            if (battlePassConfig == null || battlePassConfig.tiers == null)
                return;

            for (int i = 0; i < battlePassConfig.tiers.Count; i++)
            {
                tierFoldouts.Add(true);

                var freeList = battlePassConfig.tiers[i].freeRewards ?? new List<BattlePassView.RewardData>();
                var premiumList = battlePassConfig.tiers[i].premiumRewards ?? new List<BattlePassView.RewardData>();

                freeRewardFoldouts.Add(new List<bool>(new bool[freeList.Count]));
                premiumRewardFoldouts.Add(new List<bool>(new bool[premiumList.Count]));
            }
        }

        #endregion
    }
}
