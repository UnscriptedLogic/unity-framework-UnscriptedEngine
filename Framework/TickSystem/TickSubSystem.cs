using System;
using System.Collections.Generic;
using UnityEngine;

namespace Framework.TickSystem
{
    public static class TickSubSystem
    {
        private static readonly Dictionary<string, Ticker> tickers = new(StringComparer.Ordinal);

        public static int TickerCount => tickers.Count;

        public static Ticker GetTicker(string tickID)
        {
            TryGetTicker(tickID, out Ticker ticker);
            return ticker;
        }

        public static bool TryGetTicker(string tickID, out Ticker ticker)
        {
            ticker = null;

            if (string.IsNullOrWhiteSpace(tickID))
            {
                return false;
            }

            string normalizedTickID = tickID.Trim();

            if (TryGetRegisteredTicker(normalizedTickID, out ticker))
            {
                return true;
            }

            RegisterSceneTickers();
            return TryGetRegisteredTicker(normalizedTickID, out ticker);
        }

        public static Ticker GetOrCreateTicker(TickerDataSO tickerData, bool dontDestroyOnLoad = true)
        {
            if (!ValidateTickerData(tickerData))
            {
                return null;
            }

            if (TryGetTicker(tickerData.TickID, out Ticker ticker))
            {
                return ticker;
            }

            return CreateTicker(tickerData, dontDestroyOnLoad);
        }

        public static Ticker CreateTicker(TickerDataSO tickerData, bool dontDestroyOnLoad = true)
        {
            if (!ValidateTickerData(tickerData))
            {
                return null;
            }

            if (TryGetTicker(tickerData.TickID, out Ticker existingTicker))
            {
                return existingTicker;
            }

            GameObject tickerObject = new(tickerData.TickID);
            tickerObject.SetActive(false);

            Ticker ticker = tickerObject.AddComponent<Ticker>();
            ticker.Initialize(tickerData);

            if (dontDestroyOnLoad && Application.isPlaying)
            {
                UnityEngine.Object.DontDestroyOnLoad(tickerObject);
            }

            tickerObject.SetActive(true);
            RegisterTicker(ticker);
            return ticker;
        }

        public static bool DestroyTicker(string tickID)
        {
            if (!TryGetTicker(tickID, out Ticker ticker))
            {
                return false;
            }

            DestroyTicker(ticker);
            return true;
        }

        public static void DestroyTicker(Ticker ticker)
        {
            if (ticker == null)
            {
                return;
            }

            UnregisterTicker(ticker);

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(ticker.gameObject);
                return;
            }

            UnityEngine.Object.DestroyImmediate(ticker.gameObject);
        }

        internal static void RegisterTicker(Ticker ticker)
        {
            if (ticker == null || string.IsNullOrWhiteSpace(ticker.TickID))
            {
                return;
            }

            string tickID = ticker.TickID.Trim();

            if (tickers.TryGetValue(tickID, out Ticker existingTicker))
            {
                if (existingTicker == null)
                {
                    tickers[tickID] = ticker;
                    return;
                }

                if (existingTicker != ticker)
                {
                    Debug.LogWarning($"Multiple {nameof(Ticker)} instances registered with TickID '{tickID}'. Keeping {existingTicker.name}.", ticker);
                }

                return;
            }

            tickers.Add(tickID, ticker);
        }

        internal static void UnregisterTicker(Ticker ticker)
        {
            if (ticker == null || string.IsNullOrWhiteSpace(ticker.TickID))
            {
                return;
            }

            string tickID = ticker.TickID.Trim();

            if (tickers.TryGetValue(tickID, out Ticker registeredTicker) && registeredTicker == ticker)
            {
                tickers.Remove(tickID);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            tickers.Clear();
        }

        private static bool TryGetRegisteredTicker(string tickID, out Ticker ticker)
        {
            if (!tickers.TryGetValue(tickID, out ticker))
            {
                return false;
            }

            if (ticker != null)
            {
                return true;
            }

            tickers.Remove(tickID);
            return false;
        }

        private static void RegisterSceneTickers()
        {
            Ticker[] sceneTickers = UnityEngine.Object.FindObjectsByType<Ticker>(FindObjectsInactive.Exclude);

            for (int i = 0; i < sceneTickers.Length; i++)
            {
                RegisterTicker(sceneTickers[i]);
            }
        }

        private static bool ValidateTickerData(TickerDataSO tickerData)
        {
            if (tickerData != null && tickerData.IsValid)
            {
                return true;
            }

            Debug.LogWarning($"{nameof(TickSubSystem)} requires a valid {nameof(TickerDataSO)} with a TickID and TickRate greater than zero.");
            return false;
        }
    }
}
