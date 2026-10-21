using System.Collections.Generic;
using UnityEngine;

namespace Bozo.ModularCharacters
{
	public class BodyShapeModifier : MonoBehaviour
	{
		private OutfitSystem system;

		public string shapeName;

		public string sorting;

		public bool useScale;

		public float scaleValue = 1f;

		public float xScaleValue = 1f;

		public float yScaleValue = 1f;

		public float zScaleValue = 1f;

		public BodyShapeModifier[] counterScale;

		private Vector3 counterValue;

		public Dictionary<string, Vector3> counterSources = new Dictionary<string, Vector3>();

		public bool useXScale;

		public bool useYScale;

		public bool useZScale;

		public bool linkScaleAxis;

		public Vector2 scaleRange = new Vector2(0.5f, 2f);

		public bool usePosition;

		private Vector3 initalPosition;

		private Vector3 initalMirrorPosition;

		public float posValue;

		public float xPosValue;

		public float yPosValue;

		public float zPosValue;

		public bool useXPos;

		public bool useYPos;

		public bool useZPos;

		public Vector2 posRange = new Vector2(-0.02f, 0.02f);

		[Range(-1f, 1f)]
		public int mirrorX = 1;

		[Range(-1f, 1f)]
		public int mirrorY = 1;

		[Range(-1f, 1f)]
		public int mirrorZ = 1;

		public bool useRotation;

		public Quaternion initalRotationAxis;

		public Quaternion initalMirrorRotationAxis;

		public float rotation;

		public Vector3 rotationAxis;

		public Vector2 rotRange = new Vector2(-90f, 90f);

		public bool mirrotRot = true;

		[SerializeField]
		private bool MirrorTransform;

		private Transform mirror;

		private bool initalized;

		private bool mute;

		private BodyModData initalData;

		private BodyModData moddedData;

		private void Awake()
		{
			initalData = GetData();
		}

		private void Start()
		{
			Init();
		}

		public void Init()
		{
			if (initalized)
			{
				return;
			}
			system = GetComponentInParent<OutfitSystem>();
			initalPosition = base.transform.localPosition;
			initalRotationAxis = base.transform.localRotation;
			if ((bool)system)
			{
				if (MirrorTransform)
				{
					GetMirror();
				}
				initalized = true;
			}
		}

		public void SetScale(float x, float y, float z, float v)
		{
			xScaleValue = x;
			yScaleValue = y;
			zScaleValue = z;
			scaleValue = v;
			ApplyScale();
		}

		public void SetScale(float value)
		{
			scaleValue = value;
			ApplyScale();
		}

		public void SetPosition(float x, float y, float z)
		{
			xPosValue = x;
			yPosValue = y;
			zPosValue = z;
			ApplyPosition();
		}

		public void SetRotation(float value)
		{
			rotation = value;
			ApplyRotation();
		}

		public void Apply()
		{
			if (!initalized)
			{
				Init();
			}
			if (usePosition)
			{
				ApplyPosition();
			}
			if (useRotation)
			{
				ApplyRotation();
			}
			if (useScale)
			{
				ApplyScale();
			}
		}

		public void ApplyScale()
		{
			scaleValue = Mathf.Clamp(scaleValue, scaleRange.x, scaleRange.y);
			xScaleValue = Mathf.Clamp(xScaleValue, scaleRange.x, scaleRange.y);
			yScaleValue = Mathf.Clamp(yScaleValue, scaleRange.x, scaleRange.y);
			zScaleValue = Mathf.Clamp(zScaleValue, scaleRange.x, scaleRange.y);
			float num = 1f;
			float num2 = 1f;
			float num3 = 1f;
			if (linkScaleAxis)
			{
				if (useXScale)
				{
					num = scaleValue;
				}
				if (useYScale)
				{
					num2 = scaleValue;
				}
				if (useZScale)
				{
					num3 = scaleValue;
				}
			}
			else
			{
				if (useXScale)
				{
					num = xScaleValue;
				}
				if (useYScale)
				{
					num2 = yScaleValue;
				}
				if (useZScale)
				{
					num3 = zScaleValue;
				}
			}
			Vector3 a = new Vector3(num, num2, num3);
			Vector3 value = new Vector3(1f / num, 1f / num2, 1f / num3);
			counterValue = Vector3.one;
			foreach (Vector3 value2 in counterSources.Values)
			{
				counterValue = Vector3.Scale(counterValue, value2);
			}
			if (!mirror && MirrorTransform)
			{
				GetMirror();
			}
			base.transform.localScale = Vector3.Scale(a, counterValue);
			if ((bool)mirror)
			{
				mirror.localScale = Vector3.Scale(a, counterValue);
			}
			BodyShapeModifier[] array = counterScale;
			foreach (BodyShapeModifier bodyShapeModifier in array)
			{
				if ((bool)bodyShapeModifier)
				{
					bodyShapeModifier.counterSources[base.name] = value;
					bodyShapeModifier.ApplyScale();
				}
			}
		}

		public void ApplyPosition()
		{
			xPosValue = Mathf.Clamp(xPosValue, posRange.x, posRange.y);
			yPosValue = Mathf.Clamp(yPosValue, posRange.x, posRange.y);
			zPosValue = Mathf.Clamp(zPosValue, posRange.x, posRange.y);
			float num = 0f;
			float num2 = 0f;
			float num3 = 0f;
			if (useXPos)
			{
				num = xPosValue;
			}
			if (useYPos)
			{
				num2 = yPosValue;
			}
			if (useZPos)
			{
				num3 = zPosValue;
			}
			Vector3 vector = new Vector3(num, num2, num3);
			Vector3 vector2 = new Vector3(num * (float)mirrorX, num2 * (float)mirrorY, num3 * (float)mirrorZ);
			if (!mirror && MirrorTransform)
			{
				GetMirror();
			}
			base.transform.localPosition = vector + initalPosition;
			if ((bool)mirror)
			{
				mirror.localPosition = vector2 + initalMirrorPosition;
			}
		}

		public void ApplyRotation()
		{
			rotation = Mathf.Clamp(rotation, rotRange.x, rotRange.y);
			Vector3 vector = new Vector3(rotation * rotationAxis.x, rotation * rotationAxis.y, rotation * rotationAxis.z);
			if (!mirror && MirrorTransform)
			{
				GetMirror();
			}
			Quaternion localRotation = initalRotationAxis * Quaternion.Euler(vector);
			Quaternion localRotation2 = initalMirrorRotationAxis * Quaternion.Euler(-vector);
			if (!mirrotRot)
			{
				localRotation2 = initalMirrorRotationAxis * Quaternion.Euler(vector);
			}
			base.transform.localRotation = localRotation;
			if ((bool)mirror)
			{
				mirror.localRotation = localRotation2;
			}
		}

		private void GetMirror()
		{
			if ((bool)system)
			{
				string text = base.name;
				text = text.Replace("_l", "_r");
				if (system.GetBones().TryGetValue(text, out var value))
				{
					mirror = value;
					initalMirrorPosition = mirror.localPosition;
					initalMirrorRotationAxis = mirror.localRotation;
				}
			}
		}

		public BodyModData GetData()
		{
			return new BodyModData
			{
				scaleValue = scaleValue,
				scale = new Vector3(xScaleValue, yScaleValue, zScaleValue),
				posValue = posValue,
				position = new Vector3(xPosValue, yPosValue, zPosValue),
				rotation = rotation
			};
		}

		public void SetData(BodyModData data)
		{
			if (data != null)
			{
				scaleValue = data.scaleValue;
				xScaleValue = data.scale.x;
				yScaleValue = data.scale.y;
				zScaleValue = data.scale.z;
				posValue = data.posValue;
				xPosValue = data.position.x;
				yPosValue = data.position.y;
				zPosValue = data.position.z;
				rotation = data.rotation;
				Apply();
			}
		}

		public void SetMute(bool mute)
		{
			if (mute != this.mute)
			{
				this.mute = mute;
				if (mute)
				{
					moddedData = GetData();
					SetData(initalData);
				}
				else
				{
					SetData(moddedData);
				}
			}
		}

		private void LateUpdate()
		{
			if (usePosition)
			{
				ApplyPosition();
			}
			if (useRotation)
			{
				ApplyRotation();
			}
			if (useScale)
			{
				ApplyScale();
			}
		}
	}
}
