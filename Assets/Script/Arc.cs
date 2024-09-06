using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityOpenXR
{
    public class Arc : MonoBehaviour
    {
        public int SegmentCount = 60;
        public float Thickness = 0.01f;

        [Tooltip("The amount of time in seconds to predict the motion of the projectile.")]
        public float ArcDuration = 3.0f;

        [Tooltip("The amount of time in seconds between each segment of the projectile.")]
        public float SegmentBreak = 0.025f;

        [Tooltip("The speed at which the line segments of the arc move.")]
        public float ArcSpeed = 0.2f;

        public Material Material;

        public LayerMask TraceLayerMask;

        public bool NonTeleportAreaUnderArc { get; private set; }
        public Vector3 PlayerTeleportPositionCandidate { get; private set; }
        public Vector3 PlayerTeleportNormalCandidate { get; private set; }

        //Private data

        private Material _materialRay;
        private LineRenderer[] _lineRenderers;
        private float _arcTimeOffset = 0.0f;
        private float _prevThickness = 0.0f;
        private int _prevSegmentCount = 0;
        private bool _showArc = true;
        private Vector3 _startPos;
        private Vector3 _projectileVelocity;
        private bool _useGravity = true;
        private Transform _arcObjectsTransfrom;
        private bool _drawOnlyFirstSegmentOfArc = false;
        private bool _arcInvalid = false;
        private float _scale = 1.5f;
        private static readonly int _colorShader = Shader.PropertyToID("_Color");
        private Color _defaultColor = Color.cyan;

        private Color _validColor = new Color(0f, 1f, 0.34f, 0.45f);
        private Color _invalidColor = new Color(1f, 0f, 0.36f, 0.45f);

        //-------------------------------------------------
        void Start()
        {
            _arcTimeOffset = Time.time;
            _materialRay = new Material(Material);
            _materialRay.SetColor(_colorShader, _defaultColor);
            _materialRay.renderQueue = 3001;
        }


        //-------------------------------------------------
        void Update()
        {
            //scale arc to match player scale
            //scale = transform.lossyScale.x;
            if (Math.Abs(Thickness - _prevThickness) > Mathf.Epsilon || SegmentCount != _prevSegmentCount)
            {
                CreateLineRendererObjects();
                _prevThickness = Thickness;
                _prevSegmentCount = SegmentCount;
            }
        }


        //-------------------------------------------------
        private void CreateLineRendererObjects()
        {
            //Destroy any existing line renderer objects
            if (_arcObjectsTransfrom != null)
            {
                Destroy(_arcObjectsTransfrom.gameObject);
            }

            GameObject ArcObjectsParent = new GameObject("ArcObjects");
            _arcObjectsTransfrom = ArcObjectsParent.transform;
            _arcObjectsTransfrom.SetParent(this.transform);

            //Create new line renderer objects
            _lineRenderers = new LineRenderer[SegmentCount];

            for (int i = 0; i < SegmentCount; ++i)
            {
                GameObject NewObject = new GameObject("LineRenderer_" + i);
                NewObject.transform.SetParent(_arcObjectsTransfrom);

                _lineRenderers[i] = NewObject.AddComponent<LineRenderer>();

                _lineRenderers[i].receiveShadows = false;
                _lineRenderers[i].reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                _lineRenderers[i].lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                _lineRenderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _lineRenderers[i].material = _materialRay;
#if (UNITY_5_4)
                lineRenderers[i].SetWidth(thickness, thickness);
#else
                _lineRenderers[i].startWidth = Thickness * _scale;
                _lineRenderers[i].endWidth = Thickness * _scale;
#endif
                _lineRenderers[i].enabled = false;
            }
        }


        //-------------------------------------------------
        public void SetArcData(
            Vector3 Position,
            Vector3 Velocity,
            bool Gravity,
            bool PointerAtBadAngle,
            bool BadTeleport)
        {
            _startPos = Position;
            _projectileVelocity = Velocity;
            _useGravity = Gravity;

            if (_drawOnlyFirstSegmentOfArc && !PointerAtBadAngle)
            {
                _arcTimeOffset = Time.time;
            }

            _drawOnlyFirstSegmentOfArc = PointerAtBadAngle;
            _arcInvalid = PointerAtBadAngle || BadTeleport;
        }


        //-------------------------------------------------
        public void Show()
        {
            _showArc = true;

            if (_lineRenderers == null)
            {
                CreateLineRendererObjects();
            }
        }


        //-------------------------------------------------
        public void Hide()
        {
            //Hide the line segments if they were previously being shown
            if (_showArc)
            {
                HideLineSegments(0, SegmentCount);
            }

            _showArc = false;
        }


        //-------------------------------------------------
        // Draws each segment of the arc individually
        //-------------------------------------------------
        public bool DrawArc(out RaycastHit hitInfo)
        {
            float TimeStep = ArcDuration / SegmentCount;

            float CurrentTimeOffset = (Time.time - _arcTimeOffset) * ArcSpeed;

            //Reset the arc time offset when it has gone beyond a segment length
            if (CurrentTimeOffset > (TimeStep + SegmentBreak))
            {
                _arcTimeOffset = Time.time;
                CurrentTimeOffset = 0.0f;
            }

            float SegmentStartTime = CurrentTimeOffset;

            float ArcHitTime = FindProjectileCollision(out hitInfo);

            var RaycastNothing = ArcHitTime == float.MaxValue;

            if (RaycastNothing)
            {
                _materialRay.SetColor(_colorShader, _invalidColor);
                NonTeleportAreaUnderArc = true;
            }


            if (_drawOnlyFirstSegmentOfArc)
            {
                //Only draw first segment
                _lineRenderers[0].enabled = true;
                _lineRenderers[0].SetPosition(0, GetArcPositionAtTime(0.0f));
                _lineRenderers[0].SetPosition(1, GetArcPositionAtTime(ArcHitTime < TimeStep ? ArcHitTime : TimeStep));

                HideLineSegments(1, SegmentCount);
            }
            else
            {
                //Draw the first segment outside the loop if needed
                int LoopStartSegment = 0;

                if (SegmentStartTime > SegmentBreak)
                {
                    float FirstSegmentEndTime = CurrentTimeOffset - SegmentBreak;

                    if (ArcHitTime < FirstSegmentEndTime)
                    {
                        FirstSegmentEndTime = ArcHitTime;
                    }

                    DrawArcSegment(0, 0.0f, FirstSegmentEndTime);

                    LoopStartSegment = 1;
                }

                bool StopArc = false;
                int CurrentSegment = 0;

                if (SegmentStartTime < ArcHitTime)
                {
                    for (CurrentSegment = LoopStartSegment; CurrentSegment < SegmentCount; ++CurrentSegment)
                    {
                        //Clamp the segment end time to the arc duration
                        float SegmentEndTime = SegmentStartTime + TimeStep;

                        if (SegmentEndTime >= ArcDuration)
                        {
                            SegmentEndTime = ArcDuration;
                            StopArc = true;
                        }

                        if (SegmentEndTime >= ArcHitTime)
                        {
                            SegmentEndTime = ArcHitTime;
                            StopArc = true;
                        }

                        DrawArcSegment(CurrentSegment, SegmentStartTime, SegmentEndTime);

                        SegmentStartTime += TimeStep + SegmentBreak;

                        //If the previous end time or the next start time is beyond the duration then stop the arc
                        if (StopArc || SegmentStartTime >= ArcDuration || SegmentStartTime >= ArcHitTime)
                        {
                            break;
                        }
                    }
                }
                else
                {
                    CurrentSegment--;
                }

                //Hide the rest of the line segments
                HideLineSegments(CurrentSegment + 1, SegmentCount);
            }

            return !RaycastNothing;
        }


        //-------------------------------------------------
        private void DrawArcSegment(int index, float startTime, float endTime)
        {
            _lineRenderers[index].enabled = true;
            _lineRenderers[index].SetPosition(0, GetArcPositionAtTime(startTime));
            _lineRenderers[index].SetPosition(1, GetArcPositionAtTime(endTime));
        }


        //-------------------------------------------------
        public void SetColor(Color color)
        {
            for (int i = 0; i < SegmentCount; ++i)
            {
#if (UNITY_5_4)
                lineRenderers[i].SetColors(color, color);
#else
                _lineRenderers[i].startColor = color;
                _lineRenderers[i].endColor = color;
#endif
            }
        }


        private const int CONSTANT_MaxHits = 500; //It'll fail sometime but eh not my fault

        RaycastHit[] Hits = new RaycastHit[CONSTANT_MaxHits];

        //-------------------------------------------------
        private float FindProjectileCollision(out RaycastHit HitInfo)
        {
            float TimeStep = ArcDuration / SegmentCount;
            float SegmentStartTime = 0.0f;

            HitInfo = new RaycastHit();

            Vector3 SegmentStartPos = GetArcPositionAtTime(SegmentStartTime);

            for (int i = 0; i < SegmentCount; ++i)
            {
                float SegmentEndTime = SegmentStartTime + TimeStep;
                Vector3 SegmentEndPos = GetArcPositionAtTime(SegmentEndTime);

                var Size = Physics.RaycastNonAlloc(SegmentStartPos,
                    SegmentEndPos - SegmentStartPos,
                    Hits,
                    (SegmentEndPos - SegmentStartPos).magnitude,
                    TraceLayerMask,
                    QueryTriggerInteraction.Ignore);

                if (Size > 0)
                {
                    Array.Sort(Hits, 0, Size, new HeightComparerDesc());

                    for (int j = 0; j < Size; j++)
                    {
                        HitInfo = Hits[j];

                        if (!HitInfo.transform.CompareTag("TeleportArea") || _arcInvalid)
                        {
                            _materialRay.SetColor(_colorShader, _invalidColor);
                            DrawCross(HitInfo.point, _invalidColor, 0.5f);
                            NonTeleportAreaUnderArc = true;
                        }
                        else
                        {
                            _materialRay.SetColor(_colorShader, _validColor);
                            NonTeleportAreaUnderArc = false;
                        }

                        PlayerTeleportPositionCandidate = HitInfo.point;
                        PlayerTeleportNormalCandidate = HitInfo.normal;

                        float SegmentDistance = Vector3.Distance(SegmentStartPos, SegmentEndPos);
                        float HitTime = SegmentStartTime + (TimeStep * (HitInfo.distance / SegmentDistance));

                        return HitTime;
                    }
                }

                SegmentStartTime = SegmentEndTime;
                SegmentStartPos = SegmentEndPos;
            }

            return float.MaxValue;
        }

        public class HeightComparerDesc : IComparer<RaycastHit>
        {
            public int Compare(RaycastHit A, RaycastHit B)
            {
                if (A.point.y > B.point.y)
                {
                    return -1;
                }

                if (A.point.y < B.point.y)
                {
                    return 1;
                }

                return 0;
            }
        }

        private static void DrawCross(Vector3 Origin, Color CrossColor, float Size)
        {
            Vector3 Line1Start = Origin + (Vector3.right * Size);
            Vector3 Line1End = Origin - (Vector3.right * Size);

            Debug.DrawLine(Line1Start, Line1End, CrossColor);

            Vector3 Line2Start = Origin + (Vector3.up * Size);
            Vector3 Line2End = Origin - (Vector3.up * Size);

            Debug.DrawLine(Line2Start, Line2End, CrossColor);

            Vector3 Line3Start = Origin + (Vector3.forward * Size);
            Vector3 Line3End = Origin - (Vector3.forward * Size);

            Debug.DrawLine(Line3Start, Line3End, CrossColor);
        }


        //-------------------------------------------------
        public Vector3 GetArcPositionAtTime(float Time)
        {
            Vector3 Gravity = _useGravity ? Physics.gravity : Vector3.zero;

            Vector3 ArcPos = _startPos + ((_projectileVelocity * Time) + (0.5f * Time * Time) * Gravity) * _scale;

            return ArcPos;
        }


        //-------------------------------------------------
        private void HideLineSegments(int StartSegment, int EndSegment)
        {
            if (_lineRenderers != null)
            {
                for (int i = StartSegment; i < EndSegment; ++i)
                {
                    if (_lineRenderers.Length < i)
                    {
                        continue;
                    }

                    _lineRenderers[i].enabled = false;
                }
            }
        }

        public bool IsArcValid() => !_arcInvalid && !NonTeleportAreaUnderArc;
    }
}