using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace VFXComposer.Capabilities
{
    public sealed class CapabilitySampleRequest
    {
        public string MotionType = "linear";
        public string HitType = "single";
        public string EmissionType = "single";
        public string TimingType = "instant";
        public readonly Dictionary<string, double> Motion = new Dictionary<string, double>(StringComparer.Ordinal);
        public readonly Dictionary<string, double> Hit = new Dictionary<string, double>(StringComparer.Ordinal);
        public readonly Dictionary<string, double> Emission = new Dictionary<string, double>(StringComparer.Ordinal);
        public readonly Dictionary<string, double> Timing = new Dictionary<string, double>(StringComparer.Ordinal);
        public Vector3 Origin = Vector3.zero;
        public Vector3 Direction = Vector3.right;
        public Vector3 Target = new Vector3(6f, 0f, 0f);
        public Vector3 TargetVelocity = Vector3.zero;
        public Vector3 CollisionMin = new Vector3(-5f, -1f, -5f);
        public Vector3 CollisionMax = new Vector3(5f, 1f, 5f);
        public Vector3[] ExternalPath = new Vector3[0];
        public float ObstacleDistance = 3f;
        public float ObstacleChangeTime = -1f;
        public float ObstacleSecondDistance = 6f;
        public float CancelTime = -1f;
        public float ReleaseTime = -1f;
        public float Duration = 2f;
        public float DeltaTime = 1f / 60f;
        public uint Seed = 1;

        public double Get(Dictionary<string, double> values, string key, double fallback)
        {
            double value;
            return values != null && values.TryGetValue(key, out value) ? value : fallback;
        }
    }

    public sealed class CapabilitySampleFrame
    {
        public int Index;
        public float Time;
        public Vector3 Position;
        public Vector3 Velocity;
        public Vector3 Source;
        public Vector3 Target;
        public float Radius;
        public float Width;
        public float Progress;
        public int Stage;
    }

    public sealed class CapabilitySampleEvent
    {
        public string Type;
        public int Frame;
        public float Time;
        public int Sequence;
        public Vector3 Position;
        public Vector3 Before;
        public Vector3 After;
        public string Detail;
    }

    public sealed class CapabilitySampleTrace
    {
        public string MotionType;
        public string HitType;
        public string EmissionType;
        public string TimingType;
        public uint Seed;
        public readonly List<CapabilitySampleFrame> Frames = new List<CapabilitySampleFrame>();
        public readonly List<CapabilitySampleEvent> Events = new List<CapabilitySampleEvent>();

        public string ToCanonicalJson()
        {
            var builder = new StringBuilder(Frames.Count * 96 + Events.Count * 80 + 128);
            builder.Append("{\"emission\":\"").Append(Escape(EmissionType)).Append("\",\"events\":[");
            for (var i = 0; i < Events.Count; i++)
            {
                if (i > 0) builder.Append(',');
                var item = Events[i];
                builder.Append("{\"detail\":\"").Append(Escape(item.Detail)).Append("\",\"frame\":").Append(item.Frame)
                    .Append(",\"after\":"); AppendVector(builder, item.After);
                builder.Append(",\"before\":"); AppendVector(builder, item.Before);
                builder.Append(",\"position\":"); AppendVector(builder, item.Position);
                builder.Append(",\"sequence\":").Append(item.Sequence).Append(",\"time\":"); AppendFloat(builder, item.Time);
                builder.Append(",\"type\":\"").Append(Escape(item.Type)).Append("\"}");
            }
            builder.Append("],\"frames\":[");
            for (var i = 0; i < Frames.Count; i++)
            {
                if (i > 0) builder.Append(',');
                var item = Frames[i];
                builder.Append("{\"index\":").Append(item.Index).Append(",\"position\":"); AppendVector(builder, item.Position);
                builder.Append(",\"progress\":"); AppendFloat(builder, item.Progress);
                builder.Append(",\"radius\":"); AppendFloat(builder, item.Radius);
                builder.Append(",\"source\":"); AppendVector(builder, item.Source);
                builder.Append(",\"stage\":").Append(item.Stage).Append(",\"time\":"); AppendFloat(builder, item.Time);
                builder.Append(",\"target\":"); AppendVector(builder, item.Target);
                builder.Append(",\"velocity\":"); AppendVector(builder, item.Velocity);
                builder.Append(",\"width\":"); AppendFloat(builder, item.Width); builder.Append('}');
            }
            builder.Append("],\"hit\":\"").Append(Escape(HitType)).Append("\",\"motion\":\"").Append(Escape(MotionType))
                .Append("\",\"seed\":").Append(Seed.ToString(CultureInfo.InvariantCulture)).Append(",\"timing\":\"")
                .Append(Escape(TimingType)).Append("\"}");
            return builder.ToString();
        }

        private static void AppendVector(StringBuilder builder, Vector3 value)
        {
            builder.Append('['); AppendFloat(builder, value.x); builder.Append(','); AppendFloat(builder, value.y); builder.Append(','); AppendFloat(builder, value.z); builder.Append(']');
        }

        private static void AppendFloat(StringBuilder builder, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidOperationException("Capability traces cannot contain non-finite values.");
            if (Mathf.Abs(value) < 0.0000005f) value = 0f;
            builder.Append(value.ToString("0.######", CultureInfo.InvariantCulture));
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }
    }
}
