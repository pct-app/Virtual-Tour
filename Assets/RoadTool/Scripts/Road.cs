// @khenkel 
// parabox llc

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections.Generic;
using System.Text;
public class Road : MonoBehaviour 
{
	public bool acceptInput = false;
	public bool connectEnds = false;
	public int insertPoint = -1;
	public List<Vector3> points = new List<Vector3>();
	public float roadWidth = 1f;
	public float groundOffset = .1f;
	public float[] theta;
	public int terrainLayer = 8;

	// uv options 
	public bool swapUV = false;
	public bool flipU = true;
	public bool flipV = true;
	public Vector2 uvScale = Vector2.one;
	public Vector2 uvOffset = Vector2.zero;

	// texture
	public Material mat;

	public void Refresh()
	{
		if(points.Count < 2)
			return;

		transform.localScale = Vector3.one;

		if(!gameObject.GetComponent<MeshFilter>())
			gameObject.AddComponent<MeshFilter>();
		else
		{
			if(gameObject.GetComponent<MeshFilter>().sharedMesh != null)
				DestroyImmediate(gameObject.GetComponent<MeshFilter>().sharedMesh);
		}

		if(!gameObject.GetComponent<MeshRenderer>())
			gameObject.AddComponent<MeshRenderer>();

		List<Vector3> v = new List<Vector3>();
		List<int> t = new List<int>();

		// calculate angles for each line segment, then build out a plane for it
		int tri_index = 0;
		int segments = connectEnds ? points.Count : points.Count-1;
		theta = new float[segments];

		for(int i = 0; i < segments; i++)
		{
			Vector2 a = points[i+0].ToXZVector2();
			Vector2 b = (connectEnds && i == segments-1) ? points[0].ToXZVector2() : points[i+1].ToXZVector2();
			
			bool flip = (a.x > b.x);// ? theta[i] : -theta[i];

			Vector3 rght = flip ? new Vector3(0,0,-1) : new Vector3(0,0,1);
			Vector3 lft = flip ? new Vector3(0,0,1) : new Vector3(0,0,-1);

			theta[i] = MathExtensions.AngleRadian(a, b);

			// seg a
			v.Add(points[i] + rght * roadWidth);		
			v.Add(points[i] + lft * roadWidth);
			// seg b
			int u = (connectEnds && i == segments-1) ? 0 : i+1;
			v.Add(points[u] + rght * roadWidth);		
			v.Add(points[u] + lft * roadWidth);

			// apply angular rotation to points
			int l = v.Count-4;

			v[l+0] = v[l+0].RotateAroundPoint(points[i+0], -theta[i]);
			v[l+1] = v[l+1].RotateAroundPoint(points[i+0], -theta[i]);

			v[l+2] = v[l+2].RotateAroundPoint(points[u], -theta[i]);
			v[l+3] = v[l+3].RotateAroundPoint(points[u], -theta[i]);

			t.AddRange(new int[6]{
				tri_index + 2,
				tri_index + 1,
				tri_index + 0,
				
				tri_index + 2,
				tri_index + 3, 
				tri_index + 1
				});

			tri_index += 4;
		}	

		// join edge vertices
		if(points.Count > 2)
		{
			segments = connectEnds ? v.Count : v.Count - 4;
			for(int i = 0; i < segments; i+=4)
			{
				int p4 = (connectEnds && i == segments-4) ? 0 : i + 4;
				int p5 = (connectEnds && i == segments-4) ? 1 : i + 5;
				int p6 = (connectEnds && i == segments-4) ? 2 : i + 6;
				int p7 = (connectEnds && i == segments-4) ? 3 : i + 7;

				Vector2 leftIntercept = MathExtensions.InterceptPoint(
					v[i+0].ToXZVector2(), v[i+2].ToXZVector2(), 
					v[p4].ToXZVector2(), v[p6].ToXZVector2());

				Vector2 rightIntercept = MathExtensions.InterceptPoint(
					v[i+1].ToXZVector2(), v[i+3].ToXZVector2(), 
					v[p5].ToXZVector2(), v[p7].ToXZVector2());

				v[i+2] = leftIntercept.ToVector3();			
				v[p4] = leftIntercept.ToVector3();

				v[i+3] = rightIntercept.ToVector3();
				v[p5] = rightIntercept.ToVector3();
			}
		}

		transform.position = Vector3.zero;

		// // center pivot point and set height offset
		Vector3 cen = v.Average();
		Vector3 diff = cen - transform.position;
		transform.position = cen;

		for(int i = 0; i < v.Count; i++)
		{
			v[i] = new Vector3(v[i].x, GroundHeight(v[i]) + groundOffset, v[i].z);
			v[i] -= diff;
		}

		Mesh m = new Mesh();
		m.vertices = v.ToArray();
		m.triangles = t.ToArray();
		m.uv = CalculateUV(m.vertices);
		m.RecalculateNormals();
		gameObject.GetComponent<MeshFilter>().sharedMesh = m;
		gameObject.GetComponent<MeshRenderer>().sharedMaterial = mat;
#if UNITY_EDITOR
		Unwrapping.GenerateSecondaryUVSet(gameObject.GetComponent<MeshFilter>().sharedMesh);
#endif
	}

	public Vector2[] CalculateUV(Vector3[] vertices)
	{
		Vector2[] uvs = new Vector2[vertices.Length];

		float scale = (1f / Vector3.Distance(vertices[0], vertices[1]));
		Vector2 topLeft = Vector2.zero;

		int v = 0; // vertex iterator
		int segments = connectEnds ? points.Count : points.Count-1;
		for(int i = 0; i < segments; i++)
		{		
			Vector3 segCenter = (vertices[v+0] + vertices[v+1] + vertices[v+2] + vertices[v+3]) / 4f;

			Vector2 u0 = vertices[v+0].RotateAroundPoint(segCenter, theta[i] + (90f * Mathf.Deg2Rad) ).ToXZVector2();
			Vector2 u1 = vertices[v+1].RotateAroundPoint(segCenter, theta[i] + (90f * Mathf.Deg2Rad) ).ToXZVector2();
			Vector2 u2 = vertices[v+2].RotateAroundPoint(segCenter, theta[i] + (90f * Mathf.Deg2Rad) ).ToXZVector2();
			Vector2 u3 = vertices[v+3].RotateAroundPoint(segCenter, theta[i] + (90f * Mathf.Deg2Rad) ).ToXZVector2();

			// normalizes uv scale
			uvs[v+0] = u0 * scale;
			uvs[v+1] = u1 * scale;
			uvs[v+2] = u2 * scale;
			uvs[v+3] = u3 * scale;

			Vector2 delta = topLeft - uvs[v+0];
			uvs[v+0] += delta;
			uvs[v+1] += delta;
			uvs[v+2] += delta;
			uvs[v+3] += delta;

			topLeft = uvs[v+2];
			v += 4;
		}

		// Normalize X axis, apply to Y
		scale = 1f / uvs[1].x - uvs[0].x;
		for(int i = 0; i < uvs.Length; i++)
		{
			uvs[i] *= scale;
		}

		// optional uv modifications
		if(swapUV)
		{
			for(int i = 0; i < uvs.Length; i++)
				uvs[i] = new Vector2(uvs[i].y, uvs[i].x);
		}
			
		if(flipU)
		{
			for(int i = 0; i < uvs.Length; i++)
				uvs[i] = new Vector2(-uvs[i].x, uvs[i].y);
		}

		if(flipV)
		{
			for(int i = 0; i < uvs.Length; i++)
				uvs[i] = new Vector2(uvs[i].x, -uvs[i].y);
		}

		for(int i = 0; i < uvs.Length; i++)
		{
			uvs[i] += uvOffset;
			uvs[i] = Vector2.Scale(uvs[i], uvScale);
		}
		return uvs;
	}

	public float GroundHeight(Vector3 v)
	{
		RaycastHit ground = new RaycastHit();

		if(Physics.Raycast(v, -Vector3.up, out ground, Mathf.Infinity))//, 1 << terrainLayer))
			return ground.point.y;

		if(Physics.Raycast(v, Vector3.up, out ground, Mathf.Infinity))//, 1 << terrainLayer))
			return ground.point.y;

		// try casting from really high up next
		if(Physics.Raycast(v + Vector3.up*1000f, -Vector3.up, out ground))
			return ground.point.y;

		return v.y;
	}	
}


public static class MathExtensions
{
	public static Vector3 RotateAroundPoint(this Vector3 v, Vector3 origin, float theta)
	{
		// discard y val
		float cx = origin.x, cy = origin.z;	// origin
		float px = v.x, py = v.z;			// point

		float s = Mathf.Sin(theta);
		float c = Mathf.Cos(theta);

		// translate point back to origin:
		px -= cx;
		py -= cy;

		// rotate point
		float xnew = px * c + py * s;
		float ynew = -px * s + py * c;

		// translate point back:
		px = xnew + cx;
		py = ynew + cy;
		
		return new Vector3(px, v.y, py);
	}

	// a-b vector, c-d vector
	public static Vector2 InterceptPoint(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
	{
		float a1, b1, c1, a2, b2, c2;

		a1 = p1.y - p0.y;
		b1 = p0.x - p1.x;
		c1 = a1*p0.x + b1*p0.y;

		a2 = p3.y - p2.y;
		b2 = p2.x - p3.x;
		c2 = a2*p2.x + b2*p2.y;

		// Debug.Log(a1 + " + " + b1 + " = " + c1);
		// Debug.Log(a2 + " + " + b2 + " = " + c2);

		float det = a1*b2 - a2*b1;
		if(det == 0)
		{
#if DEBUG
			Debug.LogWarning("Lines are parallel");
#endif
			return Vector2.zero;
		}
		else
		{
			float x = (b2*c1 - b1*c2)/det;
			float y = (a1*c2 - a2*c1)/det;
			// Debug.Log("x " + x + "  y " + y);
			return new Vector2(x, y);
		}
	}

	// ....what have I done....
	public static void SlopeIntercept(Vector2 a, Vector2 c, out float m, out float b, out float x, out float y)
	{
		float y0 = (c.y-a.y);
		float x0 = (c.x-a.x);
		
		if(y0 == 0f)
		{
			x = float.NaN;
			y = a.y;
			m = float.NaN;
			b = float.NaN;
			return;
		}

		if(x0 == 0f)
		{
			x = a.x;
			y = float.NaN;
			m = float.NaN;
			b = float.NaN;
			return;
		}
		
		m = (c.y-a.y)/(c.x-a.x);
		b = (m*a.x)-a.y;

		x = float.NaN;
		y = float.NaN;
	}

	public static Vector2 ToXZVector2(this Vector3 v)
	{
		return new Vector2(v.x, v.z);
	}

	public static Vector3 ToVector3(this Vector2 v)
	{
		return new Vector3(v.x, 0f, v.y);
	}

	public static float AngleRadian(this Vector2 a, Vector2 b)
	{
		float opp = b.y - a.y;
		float adj = b.x - a.x;
		// Debug.Log(opp + " / " + adj);
		if(adj == 0)
			return Mathf.Tan(0f);
		else
			return Mathf.Atan( opp / adj );// * Mathf.Rad2Deg;
	}

	public static Vector3 Average(this List<Vector3> arr)
	{
		if(arr == null || arr.Count < 1)
			return Vector3.zero;

		Vector3 n = arr[0];
		for(int i = 1; i < arr.Count; i++)
			n += arr[i];
		return n/(float)arr.Count;
	}
}

public static class StringExtensions
{
	public static string ToFormattedString<T>(this T[] arr, string delimiter)
	{
		StringBuilder sb = new StringBuilder();
		for(int i = 0; i < arr.Length-1; i++)
			sb.Append(arr[i].ToString() + delimiter);
		sb.Append(arr[arr.Length-1]);
		return sb.ToString();
	}	
}