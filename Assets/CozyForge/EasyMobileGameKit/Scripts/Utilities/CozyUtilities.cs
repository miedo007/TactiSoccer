using System;
using UnityEngine;

namespace CozyFramework
{
    public class CozyUtilities
    {
        public static void EnableCG(CanvasGroup cg)
        {
            cg.alpha = 1;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }

        public static void DisableCG(CanvasGroup cg)
        {
            cg.alpha = 0;
            cg.interactable = false;
            cg.blocksRaycasts = false;
        }

        public static string GetDurationFromSeconds(float totalSeconds)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(totalSeconds);

            int days = timeSpan.Days;
            int hours = timeSpan.Hours;
            int minutes = timeSpan.Minutes;
            int seconds = timeSpan.Seconds;

            string formattedTime = "";

            if (days > 0)
            {
                formattedTime += days + "d ";
            }

            if (hours > 0)
            {
                formattedTime += hours + "h ";
            }

            if (minutes > 0)
            {
                formattedTime += minutes + "m ";
            }

            if (seconds > 0 || totalSeconds < 60)
            {
                formattedTime += seconds + "s";
            }

            return formattedTime.Trim();
        }

        public static string GetDurationFromSecondsDayHour(float totalSeconds)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(totalSeconds);

            int days = timeSpan.Days;
            int hours = timeSpan.Hours;

            string formattedTime = "";

            if (days > 0)
            {
                formattedTime += days + "d ";
            }

            if (hours > 0)
            {
                formattedTime += hours + "h ";
            }

            return formattedTime.Trim();
        }

        public static string FormatNumber(long quantity)
        {
            double number = quantity;
            string suffix = string.Empty;

            if (Math.Abs(number) >= 1_000_000_000_000)
            {
                number /= 1_000_000_000_000d;
                suffix = "T";
            }
            else if (Math.Abs(number) >= 1_000_000_000)
            {
                number /= 1_000_000_000d;
                suffix = "B";
            }
            else if (Math.Abs(number) >= 1_000_000)
            {
                number /= 1_000_000d;
                suffix = "M";
            }
            else if (Math.Abs(number) >= 1_000)
            {
                number /= 1_000d;
                suffix = "K";
            }
            else
            {
                return quantity.ToString();
            }

            return number.ToString("0.#") + suffix;
        }
    }
}
