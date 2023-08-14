using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using static codec.PhotoFrame.TextureBaker;
using static codec.PhotoFrame.TextureBaker.PackJob;
using static Unity.Mathematics.math;

namespace codec.PhotoFrame.Tests {
	[TestFixture]
	public class TextureBakerJobTest {
		public TextureBaker.PackJob pack;

		public void setSkyline(ushort[] skylineAll) {
			pack.skylineY.CopyFrom(skylineAll.Take(10).ToArray());
			pack.skylineXMove.CopyFrom(skylineAll.Skip(10).ToArray());
		}

		public void AssertSkyline() {
			int _s = 0; // safety limit

			int i = 0;
			bool dir = PackJob.DirRight;
			int startY = 0;
			while(_s++ < 1000) {
				if(dir == PackJob.DirRight) startY = pack.skylineY[i];
				else Assert.AreEqual(startY, pack.skylineY[i], "skyline y values on span do not match");

				if(dir == PackJob.DirRight) i += pack.skylineXMove[i];
				else i++;
				dir = !dir;

				if(i >= pack.textureSize) {
					Assert.That(i == pack.textureSize && dir == PackJob.DirRight, "skyline bad x move ending");
					break;
				}
			}
		}

		[SetUp]
		public void SetUp() {
			pack = new TextureBaker.PackJob();
			pack.textureSize = 10;
			pack.skylineXMove = new NativeArray<ushort>(10, Allocator.Persistent);
			pack.skylineY = new NativeArray<ushort>(10, Allocator.Persistent);
		}

		[TearDown]
		public void TearDown() {
			pack.skylineXMove.Dispose();
			pack.skylineY.Dispose();
		}

		[Test]
		public void TextureBaker_PackJob_s_dir_Passes() {
			Assert.AreEqual(DirLeft, pack.s_dir(-1));
			Assert.AreEqual(DirLeft, pack.s_dir(-2));

			Assert.AreEqual(DirRight, pack.s_dir(0));
			Assert.AreEqual(DirRight, pack.s_dir(1));
			Assert.AreEqual(DirRight, pack.s_dir(2));
		}

		[Test]
		public void TextureBaker_PackJob_s_posToIndex_Passes() {
			Assert.AreEqual(0, pack.s_posToIndex(-1));
			Assert.AreEqual(1, pack.s_posToIndex(-2));

			Assert.AreEqual(0, pack.s_posToIndex(1));
			Assert.AreEqual(1, pack.s_posToIndex(2));
			Assert.AreEqual(2, pack.s_posToIndex(3));
		}

		[Test]
		public void TextureBaker_PackJob_s_outside_Passes() {
			Assert.AreEqual(true, pack.s_outside(-11));
			Assert.AreEqual(false, pack.s_outside(-10));
			Assert.AreEqual(false, pack.s_outside(-9));

			Assert.AreEqual(false, pack.s_outside(-2));
			Assert.AreEqual(false, pack.s_outside(-1));

			Assert.AreEqual(true, pack.s_outside(0));

			Assert.AreEqual(false, pack.s_outside(1));
			Assert.AreEqual(false, pack.s_outside(2));

			Assert.AreEqual(false, pack.s_outside(9));
			Assert.AreEqual(false, pack.s_outside(10));
			Assert.AreEqual(true, pack.s_outside(11));
		}

		[Test]
		public void TextureBaker_PackJob_s_swapNeighbor_Passes() {
			Assert.AreEqual(int.MaxValue, pack.s_swapNeighbor(0));
			Assert.AreEqual(int.MaxValue, pack.s_swapNeighbor(1));
			Assert.AreEqual(-1, pack.s_swapNeighbor(2));
			Assert.AreEqual(-2, pack.s_swapNeighbor(3));

			Assert.AreEqual(2, pack.s_swapNeighbor(-1));
			Assert.AreEqual(3, pack.s_swapNeighbor(-2));
			Assert.AreEqual(4, pack.s_swapNeighbor(-3));

			Assert.AreEqual(int.MaxValue, pack.s_swapNeighbor(int.MaxValue));
			Assert.AreEqual(-9, pack.s_swapNeighbor(10));
			Assert.AreEqual(int.MaxValue, pack.s_swapNeighbor(-10));

			foreach(int num in new int[] { 2, 3, 4, 5, -1, -2, -3, -4, -5 }) Assert.AreEqual(num, pack.s_swapNeighbor(pack.s_swapNeighbor(num)));
		}

		[Test]
		public void TextureBaker_PackJob_s_swapSpan_Passes() {
			setSkyline(new ushort[] {
			//  1  2  3  4  5  6  7  8  9 10
				5, 0, 5, 0, 3, 3, 4, 0, 0, 4,
				2, 0, 2, 0, 1, 1, 3, 0, 0, 3,
			});
			AssertSkyline();

			Assert.AreEqual(-8, pack.s_swapSpan(3, 5));
			Assert.AreEqual(-7, pack.s_swapSpan(3, 4));

			Assert.AreEqual(2, pack.s_swapSpan(-6, 4));
			Assert.AreEqual(2, pack.s_swapSpan(-6, 4));

			Assert.AreEqual(-3, pack.s_swapSpan(1));
			Assert.AreEqual(1, pack.s_swapSpan(-3));

			Assert.AreEqual(-4, pack.s_swapSpan(4));
			Assert.AreEqual(4, pack.s_swapSpan(-4));

			Assert.AreEqual(-6, pack.s_swapSpan(5));
			Assert.AreEqual(5, pack.s_swapSpan(-6));

			Assert.AreEqual(-10, pack.s_swapSpan(7));
			Assert.AreEqual(7, pack.s_swapSpan(-10));

			Assert.AreEqual(int.MaxValue, pack.s_swapSpan(0));
			Assert.AreEqual(int.MaxValue, pack.s_swapSpan(11));
			Assert.AreEqual(int.MaxValue, pack.s_swapSpan(-11));

			Assert.AreEqual(int.MaxValue, pack.s_swapSpan(int.MaxValue));

			foreach(int num in new int[] { 1, 2, 3, 4, 5, -1, -2, -3, -4, -5 }) Assert.AreEqual(num, pack.s_swapSpan(pack.s_swapSpan(num, 7), 7));
		}

		[Test]
		public void TextureBaker_PackJob_s_y_Passes() {
			setSkyline(new ushort[] {
			//  1  2  3  4  5  6  7  8  9 10
				5, 0, 5, 0, 3, 3, 4, 0, 0, 4,
				2, 0, 2, 0, 1, 1, 3, 0, 0, 3,
			});
			AssertSkyline();

			Assert.AreEqual(pack.textureSize, pack.s_y(0));

			Assert.AreEqual(5, pack.s_y(1));
			Assert.AreEqual(5, pack.s_y(-1));

			Assert.AreEqual(0, pack.s_y(4));
			Assert.AreEqual(0, pack.s_y(-4));

			Assert.AreEqual(4, pack.s_y(10));
			Assert.AreEqual(4, pack.s_y(-10));

			Assert.AreEqual(pack.textureSize, pack.s_y(11));
			Assert.AreEqual(pack.textureSize, pack.s_y(-11));
		}

		[Test]
		public void TextureBaker_PackJob_s_spanWidth_Passes() {
			setSkyline(new ushort[] {
			//  1  2  3  4  5  6  7  8  9 10
				5, 0, 5, 0, 3, 3, 4, 0, 0, 4,
				2, 0, 2, 0, 1, 1, 3, 0, 0, 3,
			});
			AssertSkyline();

			Assert.AreEqual(0, pack.s_spanWidth(0));
			Assert.AreEqual(0, pack.s_spanWidth(100));
			Assert.AreEqual(0, pack.s_spanWidth(11));
			Assert.AreEqual(0, pack.s_spanWidth(-11));
			Assert.AreEqual(3, pack.s_spanWidth(1));
			Assert.AreEqual(3, pack.s_spanWidth(-1));
			Assert.AreEqual(3, pack.s_spanWidth(3));
			Assert.AreEqual(1, pack.s_spanWidth(4));
			Assert.AreEqual(4, pack.s_spanWidth(7));
			Assert.AreEqual(4, pack.s_spanWidth(-10));
		}

		[Test]
		public void TextureBaker_PackJob_skyline_nextSpan_Passes() {
			setSkyline(new ushort[] {
			//  1  2  3  4  5  6  7  8  9 10
				5, 0, 5, 0, 3, 3, 4, 0, 0, 4,
				2, 0, 2, 0, 1, 1, 3, 0, 0, 3,
			});
			AssertSkyline();

			Assert.AreEqual(int.MaxValue, pack.skyline_nextSpan(0));
			Assert.AreEqual(int.MaxValue, pack.skyline_nextSpan(100));
			Assert.AreEqual(int.MaxValue, pack.skyline_nextSpan(11));
			Assert.AreEqual(int.MaxValue, pack.skyline_nextSpan(-11));

			Assert.AreEqual(4, pack.skyline_nextSpan(1));
			Assert.AreEqual(5, pack.skyline_nextSpan(4));
			Assert.AreEqual(7, pack.skyline_nextSpan(5));
			Assert.AreEqual(int.MaxValue, pack.skyline_nextSpan(7));

			Assert.AreEqual(-6, pack.skyline_nextSpan(-10));
			Assert.AreEqual(-4, pack.skyline_nextSpan(-6));
			Assert.AreEqual(-3, pack.skyline_nextSpan(-4));
			Assert.AreEqual(int.MaxValue, pack.skyline_nextSpan(-3));
		}

		[Test]
		public void TextureBaker_PackJob_skyline_previousSpan_Passes() {
			setSkyline(new ushort[] {
			//  1  2  3  4  5  6  7  8  9 10
				5, 0, 5, 0, 3, 3, 4, 0, 0, 4,
				2, 0, 2, 0, 1, 1, 3, 0, 0, 3,
			});
			AssertSkyline();

			Assert.AreEqual(int.MaxValue, pack.skyline_previousSpan(0));
			Assert.AreEqual(int.MaxValue, pack.skyline_previousSpan(100));
			Assert.AreEqual(int.MaxValue, pack.skyline_previousSpan(11));
			Assert.AreEqual(int.MaxValue, pack.skyline_previousSpan(-11));

			Assert.AreEqual(5, pack.skyline_previousSpan(7));
			Assert.AreEqual(4, pack.skyline_previousSpan(5));
			Assert.AreEqual(1, pack.skyline_previousSpan(4));
			Assert.AreEqual(int.MaxValue, pack.skyline_previousSpan(1));

			Assert.AreEqual(-4, pack.skyline_previousSpan(-3));
			Assert.AreEqual(-6, pack.skyline_previousSpan(-4));
			Assert.AreEqual(-10, pack.skyline_previousSpan(-6));
			Assert.AreEqual(int.MaxValue, pack.skyline_previousSpan(-10));
		}

		[Test]
		public void TextureBaker_PackJob_skyline_traverseForward_Passes() {
			setSkyline(new ushort[] {
			//  1  2  3  4  5  6  7  8  9 10
				5, 0, 5, 0, 3, 3, 4, 0, 0, 4,
				2, 0, 2, 0, 1, 1, 3, 0, 0, 3,
			});
			AssertSkyline();

			Assert.AreEqual(int.MaxValue, pack.skyline_traverseForward(0));
			Assert.AreEqual(int.MaxValue, pack.skyline_traverseForward(100));
			Assert.AreEqual(int.MaxValue, pack.skyline_traverseForward(11));
			Assert.AreEqual(int.MaxValue, pack.skyline_traverseForward(-11));

			Assert.AreEqual(-3, pack.skyline_traverseForward(1));
			Assert.AreEqual(4, pack.skyline_traverseForward(-3));
			Assert.AreEqual(-4, pack.skyline_traverseForward(4));
			Assert.AreEqual(5, pack.skyline_traverseForward(-4));
			Assert.AreEqual(-6, pack.skyline_traverseForward(5));
			Assert.AreEqual(7, pack.skyline_traverseForward(-6));
			Assert.AreEqual(-10, pack.skyline_traverseForward(7));
			Assert.AreEqual(int.MaxValue, pack.skyline_traverseForward(-10));
		}

		[Test]
		public void TextureBaker_PackJob_skyline_traverseBackward_Passes() {
			setSkyline(new ushort[] {
			//  1  2  3  4  5  6  7  8  9 10
				5, 0, 5, 0, 3, 3, 4, 0, 0, 4,
				2, 0, 2, 0, 1, 1, 3, 0, 0, 3,
			});
			AssertSkyline();

			Assert.AreEqual(int.MaxValue, pack.skyline_traverseBackward(0));
			Assert.AreEqual(int.MaxValue, pack.skyline_traverseBackward(100));
			Assert.AreEqual(int.MaxValue, pack.skyline_traverseBackward(11));
			Assert.AreEqual(int.MaxValue, pack.skyline_traverseBackward(-11));

			Assert.AreEqual(7, pack.skyline_traverseBackward(-10));
			Assert.AreEqual(-6, pack.skyline_traverseBackward(7));
			Assert.AreEqual(5, pack.skyline_traverseBackward(-6));
			Assert.AreEqual(-4, pack.skyline_traverseBackward(5));
			Assert.AreEqual(4, pack.skyline_traverseBackward(-4));
			Assert.AreEqual(-3, pack.skyline_traverseBackward(4));
			Assert.AreEqual(1, pack.skyline_traverseBackward(-3));
			Assert.AreEqual(int.MaxValue, pack.skyline_traverseBackward(1));
		}

		[Test]
		public void TextureBaker_PackJob_skyline_setUnchecked_001_Passes() {
			setSkyline(new ushort[] {
			//  1  2  3  4  5  6  7  8  9 10
				5, 0, 5, 0, 3, 3, 4, 0, 0, 4,
				2, 0, 2, 0, 1, 1, 3, 0, 0, 3,
			});
			AssertSkyline();

			pack.skyline_setUnchecked(5, 10, 9);

			Assert.AreEqual(new ushort[] { 5, 0, 5, 0, 9, 3, 4, 0, 0, 9 }, pack.skylineY.ToArray());
			Assert.AreEqual(new ushort[] { 2, 0, 2, 0, 5, 1, 3, 0, 0, 5 }, pack.skylineXMove.ToArray());

			AssertSkyline();
		}

		[Test]
		public void TextureBaker_PackJob_skyline_setUnchecked_002_Passes() {
			setSkyline(new ushort[] {
			//  1  2  3  4  5  6  7  8  9 10
				5, 0, 5, 0, 3, 3, 4, 0, 0, 4,
				2, 0, 2, 0, 1, 1, 3, 0, 0, 3,
			});
			AssertSkyline();

			pack.skyline_setUnchecked(10, 5, 9);

			Assert.AreEqual(new ushort[] { 5, 0, 5, 0, 9, 3, 4, 0, 0, 9 }, pack.skylineY.ToArray());
			Assert.AreEqual(new ushort[] { 2, 0, 2, 0, 5, 1, 3, 0, 0, 5 }, pack.skylineXMove.ToArray());

			AssertSkyline();
		}

		[Test]
		public void TextureBaker_PackJob_skyline_getMinYPos_001_Passes() {
			setSkyline(new ushort[] {
			//  1  2  3  4  5  6  7  8  9 10
				5, 0, 5, 0, 3, 3, 4, 0, 0, 4,
				2, 0, 2, 0, 1, 1, 3, 0, 0, 3,
			});
			AssertSkyline();

			Assert.AreEqual(4, pack.skyline_getMinYPos());
		}

		[Test]
		public void TextureBaker_PackJob_skyline_getMinYPos_002_Passes() {
			setSkyline(new ushort[] {
			//  1  2  3  4  5  6  7  8  9 10
				1, 0, 1, 5, 3, 3, 4, 0, 0, 4,
				2, 0, 2, 0, 1, 1, 3, 0, 0, 3,
			});
			AssertSkyline();

			Assert.AreEqual(1, pack.skyline_getMinYPos());
		}

		[Test]
		public void TextureBaker_PackJob_skyline_getMinYPos_003_Passes() {
			setSkyline(new ushort[] {
			//  1  2  3  4  5  6  7  8  9 10
				6, 0, 6, 5, 3, 3, 1, 0, 0, 1,
				2, 0, 2, 0, 1, 1, 3, 0, 0, 3,
			});
			AssertSkyline();

			Assert.AreEqual(7, pack.skyline_getMinYPos());
		}

		[Test]
		public void TextureBaker_PackJob_skyline_getMinYPos_004_Passes() {
			setSkyline(new ushort[] {
			//  1  2  3  4  5  6  7  8  9 10
				6, 0, 6, 5, 3, 3, 4, 0, 4, 1,
				2, 0, 2, 0, 1, 1, 2, 0, 2, 0,
			});
			AssertSkyline();

			Assert.AreEqual(10, pack.skyline_getMinYPos());
		}

		[Test]
		public void TextureBaker_PackJob_skyline_addRect_001_Passes() {
			setSkyline(new ushort[] {
			//  1  2  3  4  5  6  7  8  9 10
				1, 0, 0, 0, 0, 0, 0, 0, 0, 1,
				9, 0, 0, 0, 0, 0, 0, 0, 0, 9,
			});
			AssertSkyline();

			pack.skyline_addRect(3, 3, 1);

			//                             1  2  3  4  5  6  7  8  9 10
			Assert.AreEqual(new ushort[] { 4, 0, 4, 1, 0, 0, 0, 0, 0, 1 }, pack.skylineY.ToArray());
			Assert.AreEqual(new ushort[] { 2, 0, 2, 6, 0, 0, 0, 0, 0, 6 }, pack.skylineXMove.ToArray());

			AssertSkyline();
		}

		[Test]
		public void TextureBaker_PackJob_skyline_addRect_002_Passes() {
			setSkyline(new ushort[] {
			//  1  2  3  4  5  6  7  8  9 10
				2, 0, 2, 1, 0, 0, 0, 0, 0, 1,
				2, 0, 2, 6, 0, 0, 0, 0, 0, 6,
			});
			AssertSkyline();

			pack.skyline_addRect(4, 3, 4);

			//                             1  2  3  4  5  6  7  8  9 10
			Assert.AreEqual(new ushort[] { 2, 0, 2, 4, 0, 0, 4, 1, 0, 1 }, pack.skylineY.ToArray());
			Assert.AreEqual(new ushort[] { 2, 0, 2, 3, 0, 0, 3, 2, 0, 2 }, pack.skylineXMove.ToArray());

			AssertSkyline();
		}

		[Test]
		public void TextureBaker_PackJob_skyline_addRect_003_Passes() {
			setSkyline(new ushort[] {
			//  1  2  3  4  5  6  7  8  9 10
				1, 0, 0, 0, 0, 1, 2, 0, 0, 2,
				5, 0, 0, 0, 0, 5, 3, 0, 0, 3,
			});
			AssertSkyline();

			pack.skyline_addRect(4, 3, -6);

			//                             1  2  3  4  5  6  7  8  9 10
			Assert.AreEqual(new ushort[] { 1, 1, 4, 0, 0, 4, 2, 0, 0, 2 }, pack.skylineY.ToArray());
			Assert.AreEqual(new ushort[] { 1, 1, 3, 0, 0, 3, 3, 0, 0, 3 }, pack.skylineXMove.ToArray());

			AssertSkyline();
		}

		[Test]
		public void TextureBaker_PackJob_skyline_addRect_004_Passes() {
			setSkyline(new ushort[] {
			//  1  2  3  4  5  6  7  8  9 10
				1, 0, 0, 0, 0, 0, 0, 0, 0, 1,
				9, 0, 0, 0, 0, 0, 0, 0, 0, 9,
			});
			AssertSkyline();

			pack.skyline_addRect(4, 6, -10);

			//                             1  2  3  4  5  6  7  8  9 10
			Assert.AreEqual(new ushort[] { 1, 0, 0, 0, 0, 1, 7, 0, 0, 7 }, pack.skylineY.ToArray());
			Assert.AreEqual(new ushort[] { 5, 0, 0, 0, 0, 5, 3, 0, 0, 3 }, pack.skylineXMove.ToArray());

			AssertSkyline();
		}

		[Test]
		public void TextureBaker_PackJob_skyline_addRect_005_Passes() {
			setSkyline(new ushort[] {
			//  1  2  3  4  5  6  7  8  9 10
				6, 0, 0, 6, 1, 0, 0, 0, 0, 1,
				3, 0, 0, 3, 5, 0, 0, 0, 0, 5,
			});
			AssertSkyline();

			pack.skyline_addRect(1, 5, 5);

			//                             1  2  3  4  5  6  7  8  9 10
			Assert.AreEqual(new ushort[] { 6, 0, 0, 6, 6, 1, 0, 0, 0, 1 }, pack.skylineY.ToArray());
			Assert.AreEqual(new ushort[] { 3, 0, 0, 3, 0, 4, 0, 0, 0, 4 }, pack.skylineXMove.ToArray());

			AssertSkyline();
		}

		[Test]
		public void TextureBaker_PackJob_skyline_addRect_006_Passes() {
			setSkyline(new ushort[] {
			//  1  2  3  4  5  6  7  8  9 10
				5, 0, 5, 0, 3, 3, 4, 0, 0, 4,
				2, 0, 2, 0, 1, 1, 3, 0, 0, 3,
			});
			AssertSkyline();

			pack.skyline_addRect(3, 2, -6);

			//                             1  2  3  4  5  6  7  8  9 10
			Assert.AreEqual(new ushort[] { 5, 0, 5, 5, 3, 5, 4, 0, 0, 4 }, pack.skylineY.ToArray());
			Assert.AreEqual(new ushort[] { 2, 0, 2, 2, 1, 2, 3, 0, 0, 3 }, pack.skylineXMove.ToArray());

			AssertSkyline();
		}

		[Test]
		public void TextureBaker_PackJob_skyline_addRect_007_Passes() {
			setSkyline(new ushort[] {
			//  1  2  3  4  5  6  7  8  9 10
				5, 0, 5, 0, 3, 3, 4, 0, 0, 4,
				2, 0, 2, 0, 1, 1, 3, 0, 0, 3,
			});
			AssertSkyline();

			pack.skyline_addRect(1, 2, 4);

			//                             1  2  3  4  5  6  7  8  9 10
			Assert.AreEqual(new ushort[] { 5, 0, 5, 2, 3, 3, 4, 0, 0, 4 }, pack.skylineY.ToArray());
			Assert.AreEqual(new ushort[] { 2, 0, 2, 0, 1, 1, 3, 0, 0, 3 }, pack.skylineXMove.ToArray());

			AssertSkyline();
		}

		[Test]
		public void TextureBaker_PackJob_skyline_addRect_008_Passes() {
			setSkyline(new ushort[] {
			//  1  2  3  4  5  6  7  8  9 10
				5, 0, 5, 0, 3, 3, 4, 0, 0, 4,
				2, 0, 2, 0, 1, 1, 3, 0, 0, 3,
			});
			AssertSkyline();

			pack.skyline_addRect(4, 2, -10);

			//                             1  2  3  4  5  6  7  8  9 10
			Assert.AreEqual(new ushort[] { 5, 0, 5, 0, 3, 3, 6, 0, 0, 6 }, pack.skylineY.ToArray());
			Assert.AreEqual(new ushort[] { 2, 0, 2, 0, 1, 1, 3, 0, 0, 3 }, pack.skylineXMove.ToArray());

			AssertSkyline();
		}

		[Test]
		public void TextureBaker_PackJob_skyline_fillMinY_001_Passes() {
			setSkyline(new ushort[] {
			//  1  2  3  4  5  6  7  8  9 10
				5, 0, 5, 0, 3, 3, 6, 0, 0, 6,
				2, 0, 2, 0, 1, 1, 3, 0, 0, 3,
			});
			AssertSkyline();

			pack.skyline_fillMinY(4);

			//                             1  2  3  4  5  6  7  8  9 10
			Assert.AreEqual(new ushort[] { 5, 0, 5, 3, 3, 3, 6, 0, 0, 6 }, pack.skylineY.ToArray());
			Assert.AreEqual(new ushort[] { 2, 0, 2, 2, 1, 2, 3, 0, 0, 3 }, pack.skylineXMove.ToArray());

			AssertSkyline();
		}

		[Test]
		public void TextureBaker_PackJob_skyline_fillMinY_002_Passes() {
			setSkyline(new ushort[] {
			//  1  2  3  4  5  6  7  8  9 10
				5, 0, 5, 3, 3, 3, 6, 0, 0, 6,
				2, 0, 2, 2, 1, 1, 3, 0, 0, 3,
			});
			AssertSkyline();

			pack.skyline_fillMinY(4);

			//                             1  2  3  4  5  6  7  8  9 10
			Assert.AreEqual(new ushort[] { 5, 0, 5, 3, 3, 5, 6, 0, 0, 6 }, pack.skylineY.ToArray());
			Assert.AreEqual(new ushort[] { 5, 0, 2, 2, 1, 5, 3, 0, 0, 3 }, pack.skylineXMove.ToArray());

			AssertSkyline();
		}

		[Test]
		public void TextureBaker_PackJob_skyline_fillMinY_003_Passes() {
			setSkyline(new ushort[] {
			//  1  2  3  4  5  6  7  8  9 10
				5, 0, 5, 3, 3, 5, 6, 0, 0, 6,
				5, 0, 2, 2, 1, 5, 3, 0, 0, 3,
			});
			AssertSkyline();

			pack.skyline_fillMinY(1);

			//                             1  2  3  4  5  6  7  8  9 10
			Assert.AreEqual(new ushort[] { 6, 0, 5, 3, 3, 5, 6, 0, 0, 6 }, pack.skylineY.ToArray());
			Assert.AreEqual(new ushort[] { 9, 0, 2, 2, 1, 5, 3, 0, 0, 9 }, pack.skylineXMove.ToArray());

			AssertSkyline();
		}

		[Test]
		public void TextureBaker_PackJob_skyline_fillMinY_004_Passes() {
			setSkyline(new ushort[] {
			//  1  2  3  4  5  6  7  8  9 10
				6, 0, 5, 3, 3, 5, 6, 0, 0, 6,
				9, 0, 2, 2, 1, 5, 3, 0, 0, 9,
			});
			AssertSkyline();

			pack.skyline_fillMinY(1);

			//                             1  2  3  4  5  6  7  8  9 10
			Assert.AreEqual(new ushort[] {10, 0, 5, 3, 3, 5, 6, 0, 0,10 }, pack.skylineY.ToArray());
			Assert.AreEqual(new ushort[] { 9, 0, 2, 2, 1, 5, 3, 0, 0, 9 }, pack.skylineXMove.ToArray());

			AssertSkyline();
		}

		[Test]
		public void TextureBaker_PackJob_skyline_fillMinY_005_Passes() {
			setSkyline(new ushort[] {
			//  1  2  3  4  5  6  7  8  9 10
				3, 0, 0, 3, 0, 0, 3, 0, 0, 3,
				3, 0, 0, 3, 1, 1, 3, 0, 0, 3,
			});
			AssertSkyline();

			pack.skyline_fillMinY(5);

			//                             1  2  3  4  5  6  7  8  9 10
			Assert.AreEqual(new ushort[] { 3, 0, 0, 3, 0, 0, 3, 0, 0, 3 }, pack.skylineY.ToArray());
			Assert.AreEqual(new ushort[] { 9, 0, 0, 3, 1, 1, 3, 0, 0, 9 }, pack.skylineXMove.ToArray());

			AssertSkyline();
		}

		[Test]
		public void TextureBaker_PackJob_skyline_fillMinY_006_Passes() {
			setSkyline(new ushort[] {
			//  1  2  3  4  5  6  7  8  9 10
				0, 0, 0, 0,10,10, 3, 0, 0, 3,
				3, 0, 0, 3, 1, 1, 3, 0, 0, 3,
			});
			AssertSkyline();

			pack.skyline_fillMinY(1);

			//                             1  2  3  4  5  6  7  8  9 10
			Assert.AreEqual(new ushort[] {10, 0, 0, 0,10,10, 3, 0, 0, 3 }, pack.skylineY.ToArray());
			Assert.AreEqual(new ushort[] { 5, 0, 0, 3, 1, 5, 3, 0, 0, 3 }, pack.skylineXMove.ToArray());

			AssertSkyline();
		}

		[Test]
		public void TextureBaker_PackJob_skyline_fillMinY_007_Passes() {
			setSkyline(new ushort[] {
			//  1  2  3  4  5  6  7  8  9 10
				3, 0, 0, 3,10,10, 0, 0, 0, 0,
				3, 0, 0, 3, 1, 1, 3, 0, 0, 3,
			});
			AssertSkyline();

			pack.skyline_fillMinY(7);

			//                             1  2  3  4  5  6  7  8  9 10
			Assert.AreEqual(new ushort[] { 3, 0, 0, 3,10,10, 0, 0, 0,10 }, pack.skylineY.ToArray());
			Assert.AreEqual(new ushort[] { 3, 0, 0, 3, 5, 1, 3, 0, 0, 5 }, pack.skylineXMove.ToArray());

			AssertSkyline();
		}

		[Test]
		public void TextureBaker_PackJob_skyline_doesFit_sideBounds_Passes() {
			// 1  2  3  4  5  6  7  8  9 10
			Assert.AreEqual(false, pack.skyline_doesFit_sideBounds(3, -2));
			Assert.AreEqual(false, pack.skyline_doesFit_sideBounds(3, 9));

			Assert.AreEqual(false, pack.skyline_doesFit_sideBounds(11, -10));
			Assert.AreEqual(false, pack.skyline_doesFit_sideBounds(11, 1));

			Assert.AreEqual(true, pack.skyline_doesFit_sideBounds(4, 1));
			Assert.AreEqual(true, pack.skyline_doesFit_sideBounds(1, 1));
			Assert.AreEqual(true, pack.skyline_doesFit_sideBounds(1, -1));
			Assert.AreEqual(true, pack.skyline_doesFit_sideBounds(4, -10));
			Assert.AreEqual(true, pack.skyline_doesFit_sideBounds(1, -10));
			Assert.AreEqual(true, pack.skyline_doesFit_sideBounds(1, 10));

			Assert.AreEqual(false, pack.skyline_doesFit_sideBounds(8, 4));
			Assert.AreEqual(true, pack.skyline_doesFit_sideBounds(7, 4));
			Assert.AreEqual(true, pack.skyline_doesFit_sideBounds(6, 4));
			Assert.AreEqual(true, pack.skyline_doesFit_sideBounds(5, 4));

			Assert.AreEqual(false, pack.skyline_doesFit_sideBounds(8, -7));
			Assert.AreEqual(true, pack.skyline_doesFit_sideBounds(7, -7));
			Assert.AreEqual(true, pack.skyline_doesFit_sideBounds(6, -7));
			Assert.AreEqual(true, pack.skyline_doesFit_sideBounds(5, -7));
		}

		[Test]
		public void TextureBaker_PackJob_skyline_getTopBoundForRect_Passes() {
			// 1  2  3  4  5  6  7  8  9 10
			Assert.AreEqual(10, pack.skyline_getTopBoundForRect(4, 100, 3));
			Assert.AreEqual(10, pack.skyline_getTopBoundForRect(4, 7, 3));
			Assert.AreEqual(10, pack.skyline_getTopBoundForRect(4, 6, 3));
			Assert.AreEqual(9, pack.skyline_getTopBoundForRect(4, 5, 3));
			Assert.AreEqual(8, pack.skyline_getTopBoundForRect(4, 4, 3));
			Assert.AreEqual(7, pack.skyline_getTopBoundForRect(4, 3, 3));
			Assert.AreEqual(7, pack.skyline_getTopBoundForRect(4, 2, 3));
			Assert.AreEqual(7, pack.skyline_getTopBoundForRect(4, 1, 3));

			Assert.AreEqual(10, pack.skyline_getTopBoundForRect(4, 50, 100));
			Assert.AreEqual(10, pack.skyline_getTopBoundForRect(4, 1, 100));
		}

		[Test]
		public void TextureBaker_PackJob_skyline_doesFit_topBound_Passes() {
			setSkyline(new ushort[] {
			//  1  2  3  4  5  6  7  8  9 10
				1, 0, 0, 1, 3, 3, 0, 0, 0, 0,
				3, 0, 0, 3, 1, 1, 3, 0, 0, 3,
			});
			AssertSkyline();

			// 1  2  3  4  5  6  7  8  9 10
			Assert.AreEqual(false, pack.skyline_doesFit_topBound(3, -4, 2));
			Assert.AreEqual(false, pack.skyline_doesFit_topBound(3, -4, 3));
			Assert.AreEqual(true, pack.skyline_doesFit_topBound(3, -4, 4));
			Assert.AreEqual(true, pack.skyline_doesFit_topBound(3, -4, 5));

			Assert.AreEqual(false, pack.skyline_doesFit_topBound(3, 1, 2));
			Assert.AreEqual(false, pack.skyline_doesFit_topBound(3, 1, 3));
			Assert.AreEqual(true, pack.skyline_doesFit_topBound(3, 1, 4));
			Assert.AreEqual(true, pack.skyline_doesFit_topBound(3, 1, 5));

			Assert.AreEqual(false, pack.skyline_doesFit_topBound(4, 5, 5));
			Assert.AreEqual(false, pack.skyline_doesFit_topBound(4, 5, 6));
			Assert.AreEqual(true, pack.skyline_doesFit_topBound(4, 5, 7));
			Assert.AreEqual(true, pack.skyline_doesFit_topBound(4, 5, 8));

			Assert.AreEqual(false, pack.skyline_doesFit_topBound(100, 5, 102));
			Assert.AreEqual(true, pack.skyline_doesFit_topBound(100, 5, 103));
		}

		[Test]
		public void TextureBaker_PackJob_skyline_doesOverlap_Passes() {
			setSkyline(new ushort[] {
			//  1  2  3  4  5  6  7  8  9 10
				5, 0, 5, 0, 3, 3, 4, 0, 0, 4,
				2, 0, 2, 0, 1, 1, 3, 0, 0, 3,
			});
			AssertSkyline();

			Assert.AreEqual(false, pack.skyline_doesOverlap(1, 4, out int overhangArea, out int farEndSpanPos));
			Assert.AreEqual(0, overhangArea);
			Assert.AreEqual(-4, farEndSpanPos);

			Assert.AreEqual(false, pack.skyline_doesOverlap(1, -4, out overhangArea, out farEndSpanPos));
			Assert.AreEqual(0, overhangArea);
			Assert.AreEqual(4, farEndSpanPos);

			Assert.AreEqual(true, pack.skyline_doesOverlap(2, -4, out overhangArea, out farEndSpanPos));

			Assert.AreEqual(false, pack.skyline_doesOverlap(4, 1, out overhangArea, out farEndSpanPos));
			Assert.AreEqual(5, overhangArea);
			Assert.AreEqual(-4, farEndSpanPos);

			Assert.AreEqual(false, pack.skyline_doesOverlap(5, 1, out overhangArea, out farEndSpanPos));
			Assert.AreEqual(5+2, overhangArea);
			Assert.AreEqual(-6, farEndSpanPos);

			Assert.AreEqual(false, pack.skyline_doesOverlap(5, -10, out overhangArea, out farEndSpanPos));
			Assert.AreEqual(1, overhangArea);
			Assert.AreEqual(5, farEndSpanPos);

			Assert.AreEqual(false, pack.skyline_doesOverlap(7, -10, out overhangArea, out farEndSpanPos));
			Assert.AreEqual(1+1+4, overhangArea);
			Assert.AreEqual(4, farEndSpanPos);

			Assert.AreEqual(true, pack.skyline_doesOverlap(8, -10, out overhangArea, out farEndSpanPos));

			Assert.AreEqual(false, pack.skyline_doesOverlap(2, -6, out overhangArea, out farEndSpanPos));
			Assert.AreEqual(0, overhangArea);
			Assert.AreEqual(5, farEndSpanPos);

			Assert.AreEqual(false, pack.skyline_doesOverlap(2, 5, out overhangArea, out farEndSpanPos));
			Assert.AreEqual(0, overhangArea);
			Assert.AreEqual(-6, farEndSpanPos);

			Assert.AreEqual(true, pack.skyline_doesOverlap(3, 5, out overhangArea, out farEndSpanPos));

			Assert.AreEqual(false, pack.skyline_doesOverlap(3, -6, out overhangArea, out farEndSpanPos));
			Assert.AreEqual(3, overhangArea);
			Assert.AreEqual(4, farEndSpanPos);

			Assert.AreEqual(true, pack.skyline_doesOverlap(4, -6, out overhangArea, out farEndSpanPos));
		}
	}
}
