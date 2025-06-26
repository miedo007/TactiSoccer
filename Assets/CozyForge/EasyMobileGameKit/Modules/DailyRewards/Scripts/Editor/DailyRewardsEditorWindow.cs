using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace CozyFramework
{
    public class DailyRewardsEditorWindow : EditorWindow
    {
        private string jsonSource = "";
        private DailyRewardsView.DailyRewardConfig dailyRewardsConfig;
        private List<bool> dayFoldouts = new List<bool>();
        private List<List<bool>> rewardFoldouts = new List<List<bool>>();
        private Vector2 scrollPos;

        [MenuItem("Tools/Cozy Forge/Daily Rewards Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<DailyRewardsEditorWindow>();
            window.titleContent = new GUIContent("Daily Rewards Editor");
            window.Show();
        }

        private void OnEnable()
        {
            if (dailyRewardsConfig == null)
            {
                dailyRewardsConfig = new DailyRewardsView.DailyRewardConfig();
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("DAILY REWARDS Editor", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("JSON Source:");
            jsonSource = EditorGUILayout.TextArea(jsonSource, GUILayout.Height(60));
            EditorGUILayout.Space(5);
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
            EditorGUILayout.Space(10);
            
            if (dailyRewardsConfig == null)
            {
                dailyRewardsConfig = new DailyRewardsView.DailyRewardConfig();
            }
            
            EditorGUILayout.LabelField("Daily Rewards Config", EditorStyles.boldLabel);
            dailyRewardsConfig.totalDays = EditorGUILayout.IntField("Total Days", dailyRewardsConfig.totalDays);
            dailyRewardsConfig.secondsPerDay = EditorGUILayout.IntField("Seconds Per Day", dailyRewardsConfig.secondsPerDay);
            
            if (dailyRewardsConfig.dailyRewards == null)
            {
                dailyRewardsConfig.dailyRewards = new List<List<DailyRewardsView.DailyRewardEntry>>();
            }
            
            if (GUILayout.Button("Add New Day", GUILayout.Height(25)))
            {
                AddNewDay();
            }

            EditorGUILayout.Space(10);
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            for (int dayIndex = 0; dayIndex < dailyRewardsConfig.dailyRewards.Count; dayIndex++)
            {
                EnsureDayFoldoutSize(dayIndex);

                EditorGUILayout.BeginVertical("box");
                dayFoldouts[dayIndex] = EditorGUILayout.Foldout(dayFoldouts[dayIndex], $"Day {dayIndex + 1}", true);
                if (GUILayout.Button("Remove Day", GUILayout.Width(100)))
                {
                    RemoveDay(dayIndex);
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(5);
                    break;
                }

                if (dayFoldouts[dayIndex])
                {
                    EditorGUI.indentLevel++;
                    List<DailyRewardsView.DailyRewardEntry> rewardsThisDay = dailyRewardsConfig.dailyRewards[dayIndex];
                    if (rewardsThisDay == null)
                    {
                        rewardsThisDay = new List<DailyRewardsView.DailyRewardEntry>();
                        dailyRewardsConfig.dailyRewards[dayIndex] = rewardsThisDay;
                    }

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Rewards", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("+", GUILayout.Width(25)))
                    {
                        AddNewReward(dayIndex);
                    }
                    EditorGUILayout.EndHorizontal();

                    for (int rewardIndex = 0; rewardIndex < rewardsThisDay.Count; rewardIndex++)
                    {
                        EnsureRewardFoldoutSize(dayIndex, rewardIndex);
                        var reward = rewardsThisDay[rewardIndex];

                        EditorGUILayout.BeginHorizontal();
                        rewardFoldouts[dayIndex][rewardIndex] = EditorGUILayout.Foldout(
                            rewardFoldouts[dayIndex][rewardIndex], 
                            $"Reward {rewardIndex + 1}", 
                            true
                        );
                        
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("X", GUILayout.Width(25)))
                        {
                            RemoveReward(dayIndex, rewardIndex);
                            EditorGUILayout.EndHorizontal();
                            break; 
                        }
                        EditorGUILayout.EndHorizontal();

                        if (rewardIndex < rewardsThisDay.Count && rewardFoldouts[dayIndex][rewardIndex])
                        {
                            EditorGUI.indentLevel++;
                            reward.id = EditorGUILayout.TextField("ID", reward.id);
                            reward.quantity = EditorGUILayout.IntField("Quantity", reward.quantity);
                            rewardsThisDay[rewardIndex] = reward;
                            EditorGUI.indentLevel--;
                        }

                        EditorGUILayout.Space(4);
                    }

                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }

            EditorGUILayout.EndScrollView();
        }

        #region JSON Handling

        private void LoadJSON()
        {
            try
            {
                if (!string.IsNullOrEmpty(jsonSource))
                {
                    dailyRewardsConfig = JsonConvert.DeserializeObject<DailyRewardsView.DailyRewardConfig>(jsonSource);
                    RebuildFoldouts();
                    Debug.Log("Daily Rewards config loaded from JSON.");
                }
                else
                {
                    dailyRewardsConfig = new DailyRewardsView.DailyRewardConfig();
                    Debug.Log("JSON is empty, created a new DailyRewardsConfig.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to parse JSON: " + e);
            }
        }

        private void GenerateJSON()
        {
            try
            {
                jsonSource = JsonConvert.SerializeObject(dailyRewardsConfig, Formatting.Indented);
                Debug.Log("Generated JSON from DailyRewardsConfig.");
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to generate JSON: " + e);
            }
        }

        #endregion

        #region Add/Remove Days/Rewards

        private void AddNewDay()
        {
            var newDayRewards = new List<DailyRewardsView.DailyRewardEntry>();
            dailyRewardsConfig.dailyRewards.Add(newDayRewards);
            dayFoldouts.Add(true);
            rewardFoldouts.Add(new List<bool>());
        }

        private void RemoveDay(int dayIndex)
        {
            if (dayIndex < 0 || dayIndex >= dailyRewardsConfig.dailyRewards.Count) return;
            dailyRewardsConfig.dailyRewards.RemoveAt(dayIndex);
            dayFoldouts.RemoveAt(dayIndex);
            rewardFoldouts.RemoveAt(dayIndex);
        }

        private void AddNewReward(int dayIndex)
        {
            if (dayIndex < 0 || dayIndex >= dailyRewardsConfig.dailyRewards.Count) return;

            var newReward = new DailyRewardsView.DailyRewardEntry
            {
                id = "",
                quantity = 1
            };
            dailyRewardsConfig.dailyRewards[dayIndex].Add(newReward);
            rewardFoldouts[dayIndex].Add(true);
        }

        private void RemoveReward(int dayIndex, int rewardIndex)
        {
            if (dayIndex < 0 || dayIndex >= dailyRewardsConfig.dailyRewards.Count) return;
            var dayRewards = dailyRewardsConfig.dailyRewards[dayIndex];
            if (rewardIndex < 0 || rewardIndex >= dayRewards.Count) return;

            dayRewards.RemoveAt(rewardIndex);
            rewardFoldouts[dayIndex].RemoveAt(rewardIndex);
        }

        #endregion

        #region Foldout Helpers

        private void RebuildFoldouts()
        {
            dayFoldouts.Clear();
            rewardFoldouts.Clear();

            if (dailyRewardsConfig == null || dailyRewardsConfig.dailyRewards == null)
                return;

            for (int d = 0; d < dailyRewardsConfig.dailyRewards.Count; d++)
            {
                dayFoldouts.Add(true);
                var rewards = dailyRewardsConfig.dailyRewards[d];
                var rFoldList = new List<bool>();
                for (int r = 0; r < rewards.Count; r++)
                {
                    rFoldList.Add(true);
                }
                rewardFoldouts.Add(rFoldList);
            }
        }

        private void EnsureDayFoldoutSize(int dayIndex)
        {
            while (dayFoldouts.Count <= dayIndex)
            {
                dayFoldouts.Add(true);
            }
            while (rewardFoldouts.Count <= dayIndex)
            {
                rewardFoldouts.Add(new List<bool>());
            }
        }

        private void EnsureRewardFoldoutSize(int dayIndex, int rewardIndex)
        {
            while (rewardFoldouts[dayIndex].Count <= rewardIndex)
            {
                rewardFoldouts[dayIndex].Add(true);
            }
        }

        #endregion
    }
}
