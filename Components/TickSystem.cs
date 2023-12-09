using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class TickSystem
{
    public class OnTickEventArgs : EventArgs
    {
        public int tick;
    }

    public static Ticker Create(string tickerName, float tickInterval = 0.2f)
    {
        GameObject tickerObject = new GameObject(tickerName);
        Ticker ticker = tickerObject.AddComponent<Ticker>();
        ticker.SetTickInterval(tickInterval);
        ticker.tickerName = tickerName;

        return ticker;
    }

    public class Ticker : MonoBehaviour
    {
        public string tickerName;

        private int tick;
        private float tickTimer;
        private float tickInterval;

        public event EventHandler<OnTickEventArgs> OnTick;

        private void Awake()
        {
            tick = 0;
        }

        private void Update()
        {
            tickTimer += Time.deltaTime;
            if (tickTimer >= tickInterval)
            {
                tickTimer -= tickInterval;
                tick++;

                OnTick?.Invoke(this, new OnTickEventArgs()
                {
                    tick = tick,
                });
            }
        }

        public void SetTickInterval(float tickInterval)
        {
            this.tickInterval = tickInterval;
        }

        public bool HasTickedAfter(int n)
        {
            return tick % n == 0;
        }

        public void Dispose()
        {
            Destroy(gameObject);
        }
    }
}