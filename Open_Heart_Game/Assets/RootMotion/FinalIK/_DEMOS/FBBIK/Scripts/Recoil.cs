﻿using UnityEngine;
using System.Collections;
using RootMotion.FinalIK;

namespace RootMotion.FinalIK.Demos {

	/// <summary>
	/// Procedural recoil using FBBIK.
	/// </summary>
	public class Recoil : OffsetModifier {
		
		[System.Serializable]
		public class RecoilOffset {

			[Tooltip("Offset vector for the associated effector when doing recoil.")]
			public Vector3 offset;
			
			// Linking this to an effector
			[System.Serializable]
			public class EffectorLink {
				[Tooltip("Type of the FBBIK effector to use")]
				public FullBodyBipedEffector effector;
				[Tooltip("Weight of using this effector")]
				public float weight;
			}

			[Tooltip("Linking this recoil offset to FBBIK effectors.")]
			public EffectorLink[] effectorLinks;

			// Apply offset to FBBIK effectors
			public void Apply(IKSolverFullBodyBiped solver, Quaternion rotation, float masterWeight) {
				foreach (EffectorLink e in effectorLinks) {
					solver.GetEffector(e.effector).positionOffset += rotation * (offset * masterWeight * e.weight);
				}
			}
		}

		[System.Serializable]
		public enum Handedness {
			Right,
			Left
		}

		[Tooltip("Reference to the AimIK component. Optional, only used to getting the aiming direction.")]
		public AimIK aimIK;
		[Tooltip("Which hand is holding the weapon?")]
		public Handedness handedness;
		[Tooltip("Check for 2-handed weapons.")]
		public bool twoHanded = true;
		[Tooltip("Weight curve for the recoil offsets. Recoil procedure is as long as this curve.")]
		public AnimationCurve recoilWeight;
		[Tooltip("How much is the magnitude randomized each time Recoil is called?")]
		public float magnitudeRandom = 0.1f;
		[Tooltip("How much is the rotation randomized each time Recoil is called?")]
		public Vector3 rotationRandom;
		[Tooltip("Rotating the primary hand bone for the recoil (in local space).")]

		[Space(10)]

		public Vector3 handRotationOffset;
		[Tooltip("FBBIK effector position offsets for the recoil (in aiming direction space).")]
		public RecoilOffset[] offsets;
		
		private float magnitudeMlp = 1f;
		private float endTime = -1f;
		private Quaternion handRotation, secondaryHandRelativeRotation, randomRotation;
		private float length = 1f;

		/// <summary>
		/// Starts the recoil procedure.
		/// </summary>
		public void Fire(float magnitude) {
			float rnd = magnitude * UnityEngine.Random.value * magnitudeRandom;
			magnitudeMlp = magnitude + rnd;
			
			randomRotation = Quaternion.Euler(rotationRandom * UnityEngine.Random.value);
			
			Keyframe[] keys = recoilWeight.keys;
			length = keys[keys.Length - 1].time;
			endTime = Time.time + length;
		}
		
		protected override void Start() {
			base.Start();
			
			ik.solver.OnPostUpdate += AfterFBBIK;
		}
		
		protected override void OnModifyOffset() {
			if (Time.time >= endTime) return;

			// Current weight of offset
			float w = recoilWeight.Evaluate(length - (endTime - Time.time)) * magnitudeMlp;

			// Find the rotation space of the recoil
			Quaternion lookRotation = aimIK != null? Quaternion.LookRotation(aimIK.solver.IKPosition - aimIK.solver.transform.position, ik.references.root.up): ik.references.root.rotation;
			lookRotation = randomRotation * lookRotation;

			// Apply FBBIK effector positionOffsets
			foreach (RecoilOffset offset in offsets) {
				offset.Apply(ik.solver, lookRotation, w);
			}

			// Rotation offset of the primary hand
			Quaternion rotationOffset = Quaternion.Lerp(Quaternion.identity, Quaternion.Euler(randomRotation * primaryHand.rotation * handRotationOffset), w);
			handRotation = rotationOffset * primaryHand.rotation;
				
			// Fix the secondary hand relative to the primary hand
			if (twoHanded) {
				Vector3 secondaryHandRelativePosition = primaryHand.InverseTransformPoint(secondaryHand.position);
				secondaryHandRelativeRotation = Quaternion.Inverse(primaryHand.rotation) * secondaryHand.rotation;

				Vector3 primaryHandPosition = primaryHand.position + primaryHandEffector.positionOffset;
				Vector3 secondaryHandPosition = primaryHandPosition + handRotation * secondaryHandRelativePosition;
				secondaryHandEffector.positionOffset += secondaryHandPosition - (secondaryHand.position + secondaryHandEffector.positionOffset);
			}
		}
		
		private void AfterFBBIK() {
			if (Time.time >= endTime) return;

			// Rotate the hand bones
			primaryHand.rotation = handRotation;
			if (twoHanded) secondaryHand.rotation = primaryHand.rotation * secondaryHandRelativeRotation;
		}

		// Shortcuts
		private IKEffector primaryHandEffector {
			get {
				if (handedness == Handedness.Right) return ik.solver.rightHandEffector;
				return ik.solver.leftHandEffector;
			}
		}
		
		private IKEffector secondaryHandEffector {
			get {
				if (handedness == Handedness.Right) return ik.solver.leftHandEffector;
				return ik.solver.rightHandEffector;
			}
		}
		
		private Transform primaryHand {
			get {
				return primaryHandEffector.bone;
			}
		}
		
		private Transform secondaryHand {
			get {
				return secondaryHandEffector.bone;
			}
		}
		
	}
}
