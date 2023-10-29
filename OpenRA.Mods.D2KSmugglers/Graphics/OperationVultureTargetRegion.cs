#region Copyright & License Information
/*
 * Copyright 2007-2022 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;

using OpenRA.Graphics;
using OpenRA.Primitives;

namespace OpenRA.Mods.D2KSmugglers.Graphics
{
	public class OperationVultureTargetRegion : IRenderable, IFinalizedRenderable
	{
		public WPos CenterPosition { get; }
		readonly int width;
		readonly Color colorLight;
		readonly Color colorDark1;
		readonly Color colorDark2;

		public OperationVultureTargetRegion(WPos centerPosition, int width, Color colorLight,
		 Color colorDark1,
		 Color colorDark2)
		{
			this.CenterPosition = centerPosition;
			this.width = width;
			this.colorLight = colorLight;
			this.colorDark1 = colorDark1;
			this.colorDark2 = colorDark2;
		}

		public WPos Pos => CenterPosition;
		public int ZOffset => 0;
		public bool IsDecoration => true;

		public IRenderable WithZOffset(int newOffset)
		{
			 return new OperationVultureTargetRegion(CenterPosition, width, colorLight, colorDark1, colorDark2);
		}

		public IRenderable OffsetBy(in WVec vec)
		{
			return new OperationVultureTargetRegion(CenterPosition + vec, width,
				colorLight, colorDark1, colorDark2);
		}

		public IRenderable AsDecoration() { return this; }

		public IFinalizedRenderable PrepareRender(WorldRenderer wr) { return this; }

		static IEnumerable<(double NCellsX, double NCellsY)> Rotate(IEnumerable<(double NCellsX, double NCellsY)> array)
		{
			foreach ((var x, var y) in array)
			{
				yield return (y, -x);
			}
		}

		static IEnumerable<IEnumerable<(double NCellsX, double NCellsY)>> GetBracers()
		{
			IEnumerable<(double, double)> bracerBottom = new[]
			{
				(-1.50, 4.75),
				(-1.50, 4.50),
				(1.50, 4.50),
				(1.50, 4.75),
			};

			for (var i = 0; i < 4; i++)
			{
				bracerBottom = Rotate(bracerBottom).ToList();
				yield return bracerBottom;
			}

			IEnumerable<(double, double)> bracerDiag = new[]
			{
				(-2.50, 4.25),
				(-2.50, 4.50),
				(-3.50, 4.50),
				(-4.50, 3.50),
				(-4.50, 2.50),
				(-4.25, 2.50),
			};

			for (var i = 0; i < 4; i++)
			{
				bracerDiag = Rotate(bracerDiag).ToList();
				yield return bracerDiag;
			}
		}

		static IEnumerable<IEnumerable<(double NCellsX, double NCellsY)>> GetRotationLines()
		{
			IEnumerable<(double, double)> lineAxes = new[]
			{
				(-0.5, 5.0),
				(0.5, 5.0),
			};

			IEnumerable<(double, double)> subLine = new[]
			{
				(-0.25, 4.75),
				(0.25, 4.75),
			};

			IEnumerable<(double, double)> subLine2 = new[]
			{
				(-0.25, 5.25),
				(0.25, 5.25),
			};

			IEnumerable<(double, double)> lineDiag = new[]
			{
				(-4.0, 3.5),
				(-3.5, 4.0),
			};

			IEnumerable<(double, double)> segments1 = new[]
			{
				(-1.75, 5.00),
				(-2.25, 5.00),
			};

			IEnumerable<(double, double)> segments2 = new[]
			{
				(1.75, 5.00),
				(2.25, 5.00),
			};

			IEnumerable<(double, double)>[] contours = { lineAxes, subLine, subLine2, lineDiag, segments1, segments2 };
			foreach (var contour in contours)
			{
				var c = contour;
				for (var i = 0; i < 4; i++)
				{
					c = Rotate(c).ToList();
					yield return c;
				}
			}
		}

		static IEnumerable<(WVec PosX, WVec PosY)> IterSegments(IEnumerable<(double NCellsX, double NCellsY)> source)
		{
			var isFirstStep = true;
			WVec a = default;

			foreach (var b in source.Select(t => new WVec(
					         (int)(t.NCellsX * 1024),
					         (int)(t.NCellsY * 1024),
					         0)))
			{
				if (!isFirstStep)
					yield return (a, b);

				a = b;
				isFirstStep = false;
			}
		}

		void DrawMembrane(WorldRenderer wr)
		{
			var diameter = 6.5;
			var tangent = 4.5;

			var positionsRaw = new (double NCellsX, double NCellsY)[]
			{
				(diameter, -tangent),
				(diameter, tangent),
				(tangent, diameter),
				(-tangent, diameter),
				(-diameter, tangent),
				(-diameter, -tangent),
				(-tangent, -diameter),
				(tangent, -diameter),
			};
			var colorPolygon = Color.FromArgb(32, 64, 32, 16);

			var vertices = positionsRaw.Select(x =>
				wr.Viewport.WorldToViewPx(
					wr.ScreenPosition(
						CenterPosition + new WVec(
							(int)(x.NCellsX * 1024),
							(int)(x.NCellsY * 1024),
							0))));

			var vertexArray = vertices.Select(x => new float3(x.X, x.Y, 0.0f)).ToArray();

			foreach (var i in Enumerable.Range(2, vertexArray.Length))
			{
				Game.Renderer.RgbaColorRenderer.FillTriangle(vertexArray[0],
					vertexArray[(i - 1) % 8], vertexArray[i % 8], colorPolygon);
			}
		}

		public void Render(WorldRenderer wr)
		{
			var cr = Game.Renderer.RgbaColorRenderer;

			DrawMembrane(wr);
			foreach (var contour in GetBracers())
			{
				foreach ((var posStart, var posEnd) in IterSegments(contour))
				{
					var a = wr.Viewport.WorldToViewPx(wr.ScreenPosition(CenterPosition + posStart));
					var b = wr.Viewport.WorldToViewPx(wr.ScreenPosition(CenterPosition + posEnd));
					cr.DrawLine(a, b, width, colorLight, BlendMode.Multiply);
				}
			}

			var t = ((double)DateTime.Now.Millisecond / 1000 + DateTime.Now.Second % 3) * Math.PI * 2 / 3;
			t = (1.0 + Math.Sin(t)) / 2;
			var color = Exts.ColorLerp((float)t, colorDark1, colorDark2);

			foreach (var contour in GetRotationLines())
			{
				foreach ((var posStart, var posEnd) in IterSegments(contour))
				{
					var a = wr.Viewport.WorldToViewPx(wr.ScreenPosition(CenterPosition + posStart));
					var b = wr.Viewport.WorldToViewPx(wr.ScreenPosition(CenterPosition + posEnd));
					cr.DrawLine(a, b, width, color);
				}
			}
		}

		public void RenderDebugGeometry(WorldRenderer wr) { }
		public Rectangle ScreenBounds(WorldRenderer wr) { return Rectangle.Empty; }
	}
}
