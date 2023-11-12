using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace codec.PhotoFrame.Tests {
	public class UtilsTest {
		[Test]
		public void FindCircleCircleIntersection_Generic001_Passes() {
			double2 p0 = new double2(5.456f, -3.654f);
			double2 p1 = new double2(0.432f, 5.443f);
			double2 p2 = new double2(-9.675f, 0.323f);

			double distance0 = math.distance(p0, p1);
			double distance1 = math.distance(p1, p2);

			bool inter = Utils.FindCircleCircleIntersection(p0, p2, distance0, distance1, out double2 p0Inter, out double2 p1Inter);

			Assert.AreEqual(true, inter);
			Assert.AreEqual(true, math.distance(p1, p0Inter) < 0.0001f || math.distance(p1, p1Inter) < 0.0001f);
		}

		[Test]
		public void FindCircleCircleIntersection_Generic002_Passes() {
			double2 p0 = new double2(-54.223f, 4.35f);
			double2 p1 = new double2(45.32f, 40.543f);
			double2 p2 = new double2(0.5f, 3.45f);

			double distance0 = math.distance(p0, p1);
			double distance1 = math.distance(p1, p2);

			bool inter = Utils.FindCircleCircleIntersection(p0, p2, distance0, distance1, out double2 p0Inter, out double2 p1Inter);

			Assert.AreEqual(true, inter);
			Assert.AreEqual(true, math.distance(p1, p0Inter) < 0.0001f || math.distance(p1, p1Inter) < 0.0001f);
		}

		[Test]
		public void FindCircleCircleIntersection_Touch_Passes() {
			double2 p0 = new double2(-10, 0.1f);
			double2 p1 = new double2(1.1f, 0.1f);
			double2 p2 = new double2(15.5f, 0.1f);

			double distance0 = math.distance(p0, p1);
			double distance1 = math.distance(p1, p2);

			bool inter = Utils.FindCircleCircleIntersection(p0, p2, distance0, distance1, out double2 p0Inter, out double2 p1Inter);

			Assert.AreEqual(true, inter);
			Assert.AreEqual(true, math.distance(p1, p0Inter) < 0.0001f);
			Assert.AreEqual(true, math.distance(p1, p1Inter) < 0.0001f);
		}

		[Test]
		public void FindCircleCircleIntersection_FarApart_Passes() {
			bool inter = Utils.FindCircleCircleIntersection(new double2(5.456f, -3.654f), new double2(-45.456f, 1.45f), 1.4f, 0.3f, out double2 p0Inter, out double2 p1Inter);

			Assert.AreEqual(false, inter);
		}

		[Test]
		public void FindCircleCircleIntersection_Overlap001_Passes() {
			bool inter = Utils.FindCircleCircleIntersection(new double2(5.456f, -3.654f), new double2(5.456f, -3.654f), 1.4f, 0.3f, out double2 p0Inter, out double2 p1Inter);

			Assert.AreEqual(false, inter);
		}

		[Test]
		public void FindCircleCircleIntersection_Overlap002_Passes() {
			bool inter = Utils.FindCircleCircleIntersection(new double2(5.456f, -3.654f), new double2(5.456f, -3.654f), 1.4f, 1.4f, out double2 p0Inter, out double2 p1Inter);

			Assert.AreEqual(false, inter);
		}

		[Test]
		public void FindCircleCircleIntersection_Overlap003_Passes() {
			bool inter = Utils.FindCircleCircleIntersection(new double2(5.456f, -3.654f), new double2(15.456f, -13.654f), 75.4f, 1.4f, out double2 p0Inter, out double2 p1Inter);

			Assert.AreEqual(false, inter);
		}

		[Test]
		public void GetVectorToVectorScaleRotation_Zero_Passes() {
			(float scale, float rotation) = Utils.GetVectorToVectorScaleRotation(new Vector2(0, 0), new Vector2(0, 0));

			Assert.That(scale, Is.EqualTo(0).Within(0.000001));
			Assert.That(rotation, Is.EqualTo(0).Within(0.000001));
		}

		[Test]
		public void GetVectorToVectorScaleRotation_Case001_Passes() {
			(float scale, float rotation) = Utils.GetVectorToVectorScaleRotation(new Vector2(1, 0), new Vector2(1, 0));

			Assert.That(scale, Is.EqualTo(1).Within(0.000001));
			Assert.That(rotation, Is.EqualTo(0).Within(0.000001));
		}

		[Test]
		public void GetVectorToVectorScaleRotation_Case002_Passes() {
			(float scale, float rotation) = Utils.GetVectorToVectorScaleRotation(new Vector2(0.435f, -23.453f), new Vector2(0.435f, -23.453f));

			Assert.That(scale, Is.EqualTo(1).Within(0.000001));
			Assert.That(rotation, Is.EqualTo(0).Within(0.000001));
		}
		
		[Test]
		public void GetVectorToVectorScaleRotation_Case003_Passes() {
			(float scale, float rotation) = Utils.GetVectorToVectorScaleRotation(new Vector2(2.5f, 0), new Vector2(0, 5));

			Assert.That(scale, Is.EqualTo(2).Within(0.000001));
			Assert.That(rotation, Is.EqualTo(-Mathf.PI * 0.5f).Within(0.000001));
		}

		[Test]
		public void GetVectorToVectorScaleRotation_Case004_Passes() {
			(float scale, float rotation) = Utils.GetVectorToVectorScaleRotation(new Vector2(5, 0), new Vector2(0, -2.5f));

			Assert.That(scale, Is.EqualTo(0.5f).Within(0.000001));
			Assert.That(rotation, Is.EqualTo(Mathf.PI * 0.5f).Within(0.000001));
		}

		[Test]
		public void GetVectorToVectorScaleRotation_Case005_Passes() {
			(float scale, float rotation) = Utils.GetVectorToVectorScaleRotation(new Vector2(7.1f, 0), new Vector2(-7.1f, 0));

			Assert.That(scale, Is.EqualTo(1).Within(0.000001));
			Assert.That(Mathf.Abs(rotation), Is.EqualTo(Mathf.PI).Within(0.000001));
		}

		[Test]
		public void GetVectorToVectorScaleRotation_Case006_Passes() {
			(float scale, float rotation) = Utils.GetVectorToVectorScaleRotation(new Vector2(5.4f, -2.11f), new Vector2(10.8f, -4.22f));

			Assert.That(scale, Is.EqualTo(2).Within(0.000001));
			Assert.That(rotation, Is.EqualTo(0).Within(0.000001));
		}
	}
}
