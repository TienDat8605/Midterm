#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Photon.Pun;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class JumpChargeBarTests
{
    private const string PrefabPath = "Assets/Resources/UI/JumpChargeBar.prefab";

    [Test]
    public void ResourcePrefab_HasWorldSpaceCanvasAndRequiredVisualReferences()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

        Assert.That(prefab, Is.Not.Null);
        JumpChargeBarView view = prefab.GetComponent<JumpChargeBarView>();
        Canvas canvas = prefab.GetComponent<Canvas>();
        Assert.That(view, Is.Not.Null);
        Assert.That(canvas, Is.Not.Null);
        Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.WorldSpace));
        Assert.That(canvas.sortingOrder, Is.GreaterThanOrEqualTo(100));
        Assert.That(prefab.transform.localScale, Is.EqualTo(Vector3.one * 0.01f));
        Assert.That(prefab.GetComponent<PhotonView>(), Is.Null);
        Assert.That(prefab.GetComponentInChildren<RectMask2D>(true), Is.Not.Null);

        SerializedObject serializedView = new SerializedObject(view);
        Assert.That(serializedView.FindProperty("worldCanvas").objectReferenceValue, Is.Not.Null);
        Assert.That(serializedView.FindProperty("visualRoot").objectReferenceValue, Is.Not.Null);
        Assert.That(serializedView.FindProperty("fillClip").objectReferenceValue, Is.Not.Null);
        Assert.That(serializedView.FindProperty("fillImage").objectReferenceValue, Is.Not.Null);
        Assert.That(serializedView.FindProperty("highlight").objectReferenceValue, Is.Not.Null);
    }

    [Test]
    public void SetChargeState_ClampsNormalizedFill()
    {
        JumpChargeBarView view = CreateViewInstance();
        try
        {
            SerializedObject serializedView = new SerializedObject(view);
            RectTransform fillClip =
                (RectTransform)serializedView.FindProperty("fillClip").objectReferenceValue;

            view.SetChargeState(true, -0.25f);
            Assert.That(view.NormalizedCharge, Is.EqualTo(0f));
            Assert.That(fillClip.anchorMax.x, Is.EqualTo(0f));

            view.SetChargeState(true, 1.5f);
            Assert.That(view.NormalizedCharge, Is.EqualTo(1f));
            Assert.That(fillClip.anchorMax.x, Is.EqualTo(1f));
        }
        finally
        {
            Object.DestroyImmediate(view.gameObject);
        }
    }

    [Test]
    public void ChargeColor_ProgressesFromGreenThroughYellowToRed()
    {
        Color low = JumpChargeBarView.EvaluateChargeColor(0f);
        Color middle = JumpChargeBarView.EvaluateChargeColor(0.5f);
        Color high = JumpChargeBarView.EvaluateChargeColor(1f);

        Assert.That(low.g, Is.GreaterThan(low.r));
        Assert.That(middle.r, Is.GreaterThan(0.9f));
        Assert.That(middle.g, Is.GreaterThan(0.7f));
        Assert.That(high.r, Is.GreaterThan(high.g));
        Assert.That(high.g, Is.LessThan(middle.g));
    }

    [Test]
    public void SetChargeState_HidesEntireVisualWhenNotCharging()
    {
        JumpChargeBarView view = CreateViewInstance();
        try
        {
            view.Initialize(null);
            Assert.That(view.IsVisualActive, Is.False);

            view.SetChargeState(true, 0.5f);
            Assert.That(view.IsVisualActive, Is.True);

            view.SetChargeState(false, 0.5f);
            Assert.That(view.IsVisualActive, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(view.gameObject);
        }
    }

    private static JumpChargeBarView CreateViewInstance()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab, Is.Not.Null);
        return Object.Instantiate(prefab).GetComponent<JumpChargeBarView>();
    }
}
#endif
