using System.Collections.Generic;
using UnityEngine;

namespace PinkSoft.BDS
{
    /// <summary>LiDAR 포인트 실시간 2D 시각화 + 큐 모니터.</summary>
    public sealed class LidarPointCloudViewer : MonoBehaviour
    {
        [SerializeField] int maxPoints = 2000;
        [SerializeField] float pointScale = 0.02f;
        [SerializeField] Color pointColor = Color.cyan;

        LidarHighSpeedReader? _reader;
        LidarBulletFilter? _filter;
        readonly List<GameObject> _pointPool = new();
        int _poolIndex;
        float _queueDepthSmoothed;

        public void Bind(LidarHighSpeedReader reader, LidarBulletFilter? filter = null)
        {
            _reader = reader;
            _filter = filter;
        }

        void Update()
        {
            if (_reader == null)
                return;

            _queueDepthSmoothed = Mathf.Lerp(_queueDepthSmoothed, _reader.QueueDepth, 0.1f);

            int processed = 0;
            while (processed < 500 && _reader.TryDequeue(out var point))
            {
                var (x, y) = LidarCoordinateMapper.ToLidarSpace(point);
                ShowPoint(new Vector3(x * 0.001f, y * 0.001f, 0f));
                _filter?.ProcessPoint(point);
                processed++;
            }

            _filter?.EndScanFrame();
        }

        void ShowPoint(Vector3 localPos)
        {
            while (_pointPool.Count < maxPoints)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.transform.SetParent(transform, false);
                go.transform.localScale = Vector3.one * pointScale;
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.material.color = pointColor;
                Destroy(go.GetComponent<Collider>());
                _pointPool.Add(go);
            }

            var obj = _pointPool[_poolIndex];
            obj.transform.localPosition = localPos;
            obj.SetActive(true);
            _poolIndex = (_poolIndex + 1) % _pointPool.Count;
        }

        void OnGUI()
        {
            if (_reader == null)
                return;
            GUI.Label(new Rect(10, 10, 400, 24),
                $"BDS Queue: {_queueDepthSmoothed:F0} | Bytes: {_reader.BytesReceived} | Points: {_reader.PointsParsed}");
        }
    }
}
