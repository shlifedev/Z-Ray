using UnityEngine;
using UnityEngine.UI;

namespace Lumera.ZRay.Samples.OrbitSalvage
{
    internal enum ESalvageFigure
    {
        Field,
        Ship,
        Core
    }

    // 별 지도와 두 기체 표식은 텍스처 없이 그린다. 기체가 이동해도 배경 메시를 다시 만들 필요가 없다.
    internal sealed class OrbitSalvageGraphic : Graphic
    {
        private static readonly Color32 GRID = new Color32(36, 59, 77, 255);
        private static readonly Color32 EDGE = new Color32(69, 110, 127, 255);
        private static readonly Color32 LIGHT = new Color32(187, 255, 229, 255);
        private static readonly Color32 TEAL = new Color32(64, 205, 178, 255);
        private static readonly Color32 GOLD = new Color32(255, 202, 111, 255);

        public ESalvageFigure Figure { get; set; }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = rectTransform.rect;
            if (Figure == ESalvageFigure.Field)
            {
                DrawField(mesh, rect);
                return;
            }

            float unit = rect.width * 0.5f;
            if (Figure == ESalvageFigure.Ship)
            {
                Vector2 nose = new Vector2(0.0f, unit);
                Vector2 left = new Vector2(-0.78f, -0.66f) * unit;
                Vector2 right = new Vector2(0.78f, -0.66f) * unit;
                Vector2 tail = new Vector2(0.0f, -0.38f) * unit;
                Triangle(mesh, nose, left, tail, LIGHT);
                Triangle(mesh, nose, tail, right, TEAL);
                Triangle(mesh, new Vector2(-0.23f, -0.48f) * unit, new Vector2(0.23f, -0.48f) * unit, Vector2.down * unit, new Color32(84, 165, 255, 255));
                Triangle(mesh, new Vector2(0.0f, 0.38f) * unit, new Vector2(-0.15f, -0.16f) * unit, new Vector2(0.15f, -0.16f) * unit, GRID);
            }
            else
            {
                float r = unit * 0.57f;
                Vector2 top = new Vector2(0.0f, r);
                Vector2 left = new Vector2(-0.86f * r, r * 0.50f);
                Vector2 right = new Vector2(-left.x, left.y);
                Quad(mesh, top, left, Vector2.zero, right, GOLD);
                Quad(mesh, left, -right, -top, Vector2.zero, new Color32(226, 137, 65, 255));
                Quad(mesh, Vector2.zero, -top, -left, right, new Color32(176, 93, 54, 255));
                Ring(mesh, Vector2.zero, unit * 0.84f, 0.0f, 360.0f, 24, 1.2f, new Color32(188, 139, 81, 255));
            }
        }

        private static void DrawField(VertexHelper mesh, Rect rect)
        {
            Quad(mesh, rect.min, new Vector2(rect.xMin, rect.yMax), rect.max, new Vector2(rect.xMax, rect.yMin), new Color32(16, 29, 45, 255));
            // 같은 화면 크기에서는 같은 별 지도를 보인다. 게임의 난수 상태는 쓰지 않는다.
            for (int index = 0; index < 115; index++)
            {
                float x = Mathf.Repeat(0.37f + index * 0.618034f, 1.0f);
                float y = Mathf.Repeat(0.18f + (index + 2) * (index + 1) * 0.03719f, 1.0f);
                Vector2 star = rect.min + Vector2.Scale(rect.size, new Vector2(x, y));
                float radius = index % 7 == 0 ? 1.65f : 0.85f;
                Triangle(mesh, star + Vector2.left * radius, star + Vector2.up * radius, star + Vector2.right * radius, EDGE);
                Triangle(mesh, star + Vector2.left * radius, star + Vector2.down * radius, star + Vector2.right * radius, EDGE);
            }

            // 반원 궤도와 가스 행성은 항법 화면의 거리 기준이다.
            Vector2 planet = rect.min + Vector2.Scale(rect.size, new Vector2(0.77f, 0.67f));
            float radiusPlanet = rect.height * 0.17f;
            Ring(mesh, planet, radiusPlanet * 1.55f, 12.0f, 272.0f, 56, 1.0f, GRID);
            Ring(mesh, planet, radiusPlanet * 1.82f, 155.0f, 352.0f, 50, 1.0f, GRID);
            for (int index = 0; index < 48; index++)
            {
                Triangle(mesh, planet, planet + Circle(radiusPlanet, index * 7.5f), planet + Circle(radiusPlanet, (index + 1) * 7.5f), new Color32(28, 52, 65, 255));
            }

            for (int index = -4; index <= 4; index++)
            {
                float y = radiusPlanet * index * 0.21f;
                float extent = Mathf.Sqrt(radiusPlanet * radiusPlanet - y * y);
                Stroke(mesh, planet + new Vector2(-extent, y), planet + new Vector2(extent, y), Mathf.Max(1.0f, radiusPlanet * 0.055f), GRID);
            }

            Ring(mesh, planet, radiusPlanet, 25.0f, 160.0f, 28, 1.7f, EDGE);
            Vector2 lowLeft = rect.min;
            Vector2 highLeft = new Vector2(rect.xMin, rect.yMax);
            Vector2 highRight = rect.max;
            Vector2 lowRight = new Vector2(rect.xMax, rect.yMin);
            Stroke(mesh, lowLeft, highLeft, 1.0f, GRID);
            Stroke(mesh, highLeft, highRight, 1.0f, GRID);
            Stroke(mesh, highRight, lowRight, 1.0f, GRID);
            Stroke(mesh, lowRight, lowLeft, 1.0f, GRID);
            for (int index = 0; index <= 16; index++)
            {
                float x = rect.xMin + rect.width * index / 16.0f;
                float length = index % 4 == 0 ? 8.0f : 4.0f;
                Stroke(mesh, new Vector2(x, rect.yMin), new Vector2(x, rect.yMin + length), 1.0f, EDGE);
                Stroke(mesh, new Vector2(x, rect.yMax), new Vector2(x, rect.yMax - length), 1.0f, EDGE);
            }
        }

        private static Vector2 Circle(float radius, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return radius * new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        private static void Ring(VertexHelper mesh, Vector2 center, float radius, float from, float to, int count, float width, Color32 color)
        {
            Vector2 previous = center + Circle(radius, from);
            for (int index = 1; index <= count; index++)
            {
                Vector2 point = center + Circle(radius, Mathf.Lerp(from, to, (float)index / count));
                Stroke(mesh, previous, point, width, color);
                previous = point;
            }
        }

        private static void Stroke(VertexHelper mesh, Vector2 from, Vector2 to, float width, Color32 color)
        {
            Vector2 offset = Vector2.Perpendicular((to - from).normalized) * (width * 0.5f);
            Quad(mesh, from + offset, to + offset, to - offset, from - offset, color);
        }

        private static void Triangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Color32 color)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(a, color, Vector2.zero);
            mesh.AddVert(b, color, Vector2.zero);
            mesh.AddVert(c, color, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
        }

        private static void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 color)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(a, color, Vector2.zero);
            mesh.AddVert(b, color, Vector2.zero);
            mesh.AddVert(c, color, Vector2.zero);
            mesh.AddVert(d, color, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
