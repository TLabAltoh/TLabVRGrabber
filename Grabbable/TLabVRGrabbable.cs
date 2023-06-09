using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class TLabCashTransform
{
    public Vector3 LocalPosiiton
    {
        get
        {
            return localPosition;
        }
    }

    public Vector3 LocalScale
    {
        get
        {
            return localScale;
        }
    }

    public Quaternion LocalRotation
    {
        get
        {
            return localRotation;
        }
    }

    public TLabCashTransform(Vector3 localPosition, Vector3 localScale, Quaternion localRotation)
    {
        this.localPosition  = localPosition;
        this.localRotation  = localRotation;
        this.localScale     = localScale;
    }

    private Vector3 localPosition;
    private Vector3 localScale;
    private Quaternion localRotation;
}

public class TLabVRGrabbable : MonoBehaviour
{
    public const int PARENT_LENGTH = 2;

    [Header("Rigidbody Setting")]

    [Tooltip("Rigidbodyを使用するか")]
    [SerializeField] protected bool m_useRigidbody = true;

    [Tooltip("RigidbodyのUseGravityを有効化するか")]
    [SerializeField] protected bool m_useGravity = false;

    [Header("Transform update settings")]

    [Tooltip("掴んでいる間，オブジェクトのポジションを更新するか")]
    [SerializeField] protected bool m_positionFixed = true;

    [Tooltip("掴んでいる間，オブジェクトのローテーションを更新するか")]
    [SerializeField] protected bool m_rotateFixed = true;

    [Tooltip("両手で掴んでいる間，オブジェクトのスケールを更新するか")]
    [SerializeField] protected bool m_scaling = true;

    [Header("Scaling Factor")]
    [Tooltip("オブジェクトのスケールの更新の感度")]
    [SerializeField, Range(0.0f, 0.25f)] protected float m_scalingFactor;

    [Header("Divided Settings")]
    [Tooltip("このコンポーネントが子階層にGrabberを束ねているか")]
    [SerializeField] protected bool m_enableDivide = false;
    [SerializeField] protected GameObject[] m_divideAnchors;

    protected GameObject m_mainParent;
    protected GameObject m_subParent;

    protected Vector3 m_mainPositionOffset;
    protected Vector3 m_subPositionOffset;

    protected Quaternion m_mainQuaternionStart;
    protected Quaternion m_thisQuaternionStart;

    protected Rigidbody m_rb;

    protected float m_scaleInitialDistance = -1.0f;
    protected float m_scalingFactorInvert;
    protected Vector3 m_scaleInitial;

    protected List<TLabCashTransform> m_cashTransforms = new List<TLabCashTransform>();

    private const string thisName = "[tlabvrgrabbable] ";

    public bool Grabbed
    {
        get
        {
            return m_mainParent != null;
        }
    }

    public bool EnableDivide
    {
        get
        {
            return m_enableDivide;
        }
    }

    public GameObject[] DivideAnchors
    {
        get
        {
            return m_divideAnchors;
        }
    }

#if UNITY_EDITOR
    public virtual void InitializeRotatable()
    {
        if (EditorApplication.isPlaying == true) return;

        m_useGravity = false;
    }

    public virtual void UseRigidbody(bool rigidbody, bool gravity)
    {
        if (EditorApplication.isPlaying == true) return;

        m_useRigidbody  = rigidbody;
        m_useGravity    = gravity;
    }
#endif

    public virtual void SetGravity(bool active)
    {
        if (m_rb == null || m_useRigidbody == false || m_useGravity == false) return;

        if (active == true)
        {
            m_rb.isKinematic = false;
            m_rb.useGravity = true;
        }
        else
        {
            m_rb.isKinematic = true;
            m_rb.useGravity = false;
            m_rb.interpolation = RigidbodyInterpolation.Interpolate;
        }
    }

    protected virtual void RbGripSwitch(bool grip)
    {
        SetGravity(!grip);
    }

    protected virtual void MainParentGrabbStart()
    {
        m_mainPositionOffset    = m_mainParent.transform.InverseTransformPoint(this.transform.position);
        m_mainQuaternionStart   = m_mainParent.transform.rotation;
        m_thisQuaternionStart   = this.transform.rotation;
    }

    protected virtual void SubParentGrabStart()
    {
        m_subPositionOffset = m_subParent.transform.InverseTransformPoint(this.transform.position);
    }

    public virtual bool AddParent(GameObject parent)
    {
        if (m_mainParent == null)
        {
            RbGripSwitch(true);

            m_mainParent = parent;

            MainParentGrabbStart();

            Debug.Log(thisName + parent.ToString() + " mainParent added");
            return true;
        }
        else if(m_subParent == null)
        {
            m_subParent = parent;

            SubParentGrabStart();

            Debug.Log(thisName + parent.ToString() + " subParent added");
            return true;
        }

        Debug.Log(thisName + "cannot add parent");
        return false;
    }

    public virtual bool RemoveParent(GameObject parent)
    {
        if(m_mainParent == parent)
        {
            if(m_subParent != null)
            {
                m_mainParent = m_subParent;
                m_subParent = null;

                MainParentGrabbStart();

                Debug.Log(thisName + "m_main released and m_sub added");

                return true;
            }
            else
            {
                RbGripSwitch(false);
                SetGravity(true);

                m_mainParent = null;

                Debug.Log(thisName + "m_main released");

                return true;
            }
        }
        else if(m_subParent == parent)
        {
            m_subParent = null;

            MainParentGrabbStart();

            Debug.Log(thisName + "m_sub released");

            return true;
        }

        return false;
    }

    protected virtual void UpdateScale()
    {
        Vector3 positionMain    = m_mainParent.transform.TransformPoint(m_mainPositionOffset);
        Vector3 positionSub     = m_subParent.transform.TransformPoint(m_subPositionOffset);

        // この処理の最初の実行時，必ずpositionMainとpositionSubは同じ座標になる
        // 拡縮の基準が小さくなりすぎてしまい，不都合
        // ---> 手の位置に座標を補間して，2つの座標を意図的にずらす

        Vector3 scalingPositionMain = m_mainParent.transform.position * m_scalingFactorInvert + positionMain * m_scalingFactor;
        Vector3 scalingPositionSub  = m_subParent.transform.position * m_scalingFactorInvert + positionSub * m_scalingFactor;

        if (m_scaleInitialDistance == -1.0f)
        {
            m_scaleInitialDistance  = (scalingPositionMain - scalingPositionSub).magnitude;
            m_scaleInitial          = this.transform.localScale;
        }
        else
        {
            float scaleRatio            = (scalingPositionMain - scalingPositionSub).magnitude / m_scaleInitialDistance;
            this.transform.localScale   = scaleRatio * m_scaleInitial;

            if (m_useRigidbody == true)
                m_rb.MovePosition(positionMain * 0.5f + positionSub * 0.5f);
            else
                this.transform.position = positionMain * 0.5f + positionSub * 0.5f;
        }
    }

    protected virtual void UpdatePosition()
    {
        if (m_useRigidbody)
        {
            if (m_positionFixed) m_rb.MovePosition(m_mainParent.transform.TransformPoint(m_mainPositionOffset));

            if (m_rotateFixed)
            {
                // https://qiita.com/yaegaki/items/4d5a6af1d1738e102751
                Quaternion deltaQuaternion = Quaternion.identity * m_mainParent.transform.rotation * Quaternion.Inverse(m_mainQuaternionStart);
                m_rb.MoveRotation(deltaQuaternion * m_thisQuaternionStart);
            }
        }
        else
        {
            if (m_positionFixed) this.transform.position = m_mainParent.transform.TransformPoint(m_mainPositionOffset);

            if (m_rotateFixed)
            {
                // https://qiita.com/yaegaki/items/4d5a6af1d1738e102751
                Quaternion deltaQuaternion = Quaternion.identity * m_mainParent.transform.rotation * Quaternion.Inverse(m_mainQuaternionStart);
                this.transform.rotation = deltaQuaternion * m_thisQuaternionStart;
            }
        }
    }

    protected virtual void CreateCombineMeshCollider()
    {
        // 自分自身のメッシュフィルターを取得
        MeshFilter meshFilter = this.gameObject.GetComponent<MeshFilter>();
        if (meshFilter == null) meshFilter = this.gameObject.AddComponent<MeshFilter>();

        // 子オブジェクトからメッシュフィルターを取得
        MeshFilter[] meshFilters = this.gameObject.GetComponentsInChildren<MeshFilter>();

        //
        List<MeshFilter> meshFilterList = new List<MeshFilter>();
        for (int i = 1; i < meshFilters.Length; i++)
        {
            if (meshFilters[i] == meshFilter) continue;
            meshFilterList.Add(meshFilters[i]);
        }

        CombineInstance[] combine = new CombineInstance[meshFilterList.Count];

        for (int i = 0; i < meshFilterList.Count; i++)
        {
            combine[i].mesh         = meshFilterList[i].sharedMesh;
            combine[i].transform    = this.gameObject.transform.worldToLocalMatrix * meshFilterList[i].transform.localToWorldMatrix;
        }

        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.CombineMeshes(combine);
        meshFilter.sharedMesh = mesh;

        MeshCollider meshCollider = GetComponent<MeshCollider>();
        if (meshCollider == null) meshCollider = this.gameObject.AddComponent<MeshCollider>();

        meshCollider.sharedMesh = meshFilter.sharedMesh;
    }

    protected virtual void Devide(bool active)
    {
        if (m_enableDivide == false) return;

        MeshCollider meshCollider = this.gameObject.GetComponent<MeshCollider>();

        if (meshCollider == null) return;

        meshCollider.enabled = !active;
        MeshCollider[] childs = this.gameObject.GetComponentsInChildren<MeshCollider>();
        for (int i = 0; i < childs.Length; i++)
        {
            if (childs[i] == meshCollider) continue;

            childs[i].enabled = active;

            if(active == false)
            {
                TLabVRRotatable[] rotatebles = this.gameObject.GetComponentsInChildren<TLabVRRotatable>();
                for(int j = 0; j < rotatebles.Length; j++)
                {
                    if (rotatebles[j].gameObject == this.gameObject) continue;

                    rotatebles[i].SetHandAngulerVelocity(Vector3.zero, 0.0f);
                }
            }
            else
            {
                TLabVRRotatable rotateble = this.gameObject.GetComponent<TLabVRRotatable>();
                if (rotateble != null) rotateble.SetHandAngulerVelocity(Vector3.zero, 0.0f);
            }
        }

        if (active == false) CreateCombineMeshCollider();
    }

    public virtual int Devide()
    {
        if (m_enableDivide == false) return -1;

        MeshCollider meshCollider = this.gameObject.GetComponent<MeshCollider>();

        if (meshCollider == null) return -1;

        bool current = meshCollider.enabled;

        Devide(current);

        return current ? 0 : 1;
    }

    public virtual void GetInitialChildTransform()
    {
        m_cashTransforms.Clear();
        Transform[] childTransforms = this.gameObject.GetComponentsInChildren<Transform>();
        foreach(Transform childTransform in childTransforms)
        {
            if (childTransform == this.transform) continue;

            m_cashTransforms.Add(new TLabCashTransform(childTransform.localPosition, childTransform.localScale, childTransform.localRotation));
        }
    }

    public virtual void SetInitialChildTransform()
    {
        if (m_enableDivide == false) return;

        int index = 0;

        Transform[] childTransforms = this.gameObject.GetComponentsInChildren<Transform>();
        foreach (Transform childTransform in childTransforms)
        {
            if (childTransform == this.transform) continue;

            TLabCashTransform cashTransform = m_cashTransforms[index++];

            childTransform.localPosition    = cashTransform.LocalPosiiton;
            childTransform.localRotation    = cashTransform.LocalRotation;
            childTransform.localScale       = cashTransform.LocalScale;
        }

        MeshCollider meshCollider = this.gameObject.GetComponent<MeshCollider>();

        if (meshCollider == null) return;

        if (meshCollider.enabled == true) CreateCombineMeshCollider();
    }

#if UNITY_EDITOR
    protected virtual void TestFunc()
    {
        Debug.Log(thisName + "Befor Override");
    }
#endif

    protected virtual void Start()
    {
        if (m_enableDivide == true)
        {
            GetInitialChildTransform();
            CreateCombineMeshCollider();
        }

        if (m_useRigidbody == true)
        {
            m_rb = GetComponent<Rigidbody>();
            if(m_rb == null) m_rb = this.gameObject.AddComponent<Rigidbody>();

            if(m_useGravity == false)
            {
                m_rb.isKinematic = true;
                m_rb.useGravity = false;
                m_rb.interpolation = RigidbodyInterpolation.Interpolate;
            }
            else
            {
                m_rb.isKinematic = false;
                m_rb.useGravity = true;
            }

            SetGravity(m_useGravity);
        }

        m_scalingFactorInvert = 1 - m_scalingFactor;

#if UNITY_EDITOR
        // TestFunc();
#endif
    }

    protected virtual void Update()
    {
        if(m_mainParent != null)
        {
            if(m_subParent != null && m_scaling)
                UpdateScale();
            else
            {
                m_scaleInitialDistance = -1.0f;

                UpdatePosition();
            }
        }
        else
            m_scaleInitialDistance = -1.0f;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(TLabVRGrabbable))]
[CanEditMultipleObjects]

public class TLabVRGrabbableEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        serializedObject.Update();

        TLabVRGrabbable grabbable = target as TLabVRGrabbable;

        TLabVRRotatable rotatable = grabbable.gameObject.GetComponent<TLabVRRotatable>();

        if (rotatable != null && GUILayout.Button("Initialize for Rotatable"))
        {
            grabbable.InitializeRotatable();
            EditorUtility.SetDirty(grabbable);
            EditorUtility.SetDirty(rotatable);
        }

        if (grabbable.EnableDivide == true && GUILayout.Button("Initialize for Devibable"))
        {
            // Grabbable
            // RigidbodyのUseGravityを無効化する
            grabbable.UseRigidbody(false, false);

            if (grabbable.EnableDivide == true)
            {
                // If grabbable is enable devide
                MeshFilter meshFilter = grabbable.GetComponent<MeshFilter>();
                if (meshFilter == null) grabbable.gameObject.AddComponent<MeshFilter>();
            }
            else
            {
                MeshFilter meshFilter = grabbable.GetComponent<MeshFilter>();
                if (meshFilter != null) Destroy(meshFilter);

                MeshRenderer meshRenderer = grabbable.GetComponent<MeshRenderer>();
                if (meshRenderer != null) Destroy(meshRenderer);
            }

            // SetLayerMask
            grabbable.gameObject.layer = LayerMask.NameToLayer("TLabGrabbable");

            // Rotatable
            if (rotatable == null) grabbable.gameObject.AddComponent<TLabSyncRotatable>();

            // MeshCollider
            MeshCollider meshCollider = grabbable.gameObject.GetComponent<MeshCollider>();
            if (meshCollider == null)
                meshCollider = grabbable.gameObject.AddComponent<MeshCollider>();
            meshCollider.enabled = true;

            EditorUtility.SetDirty(grabbable);
            EditorUtility.SetDirty(rotatable);

            // Childlen

            foreach (GameObject divideAnchor in grabbable.DivideAnchors)
                foreach (Transform grabbableChildTransform in divideAnchor.GetComponentsInChildren<Transform>())
                {
                    if (grabbableChildTransform.gameObject == divideAnchor.gameObject)  continue;
                    if (grabbableChildTransform.gameObject.activeSelf == false)         continue;

                    // Grabbable
                    TLabVRGrabbable grabbableChild = grabbableChildTransform.gameObject.GetComponent<TLabVRGrabbable>();
                    if (grabbableChild == null)
                        grabbableChild = grabbableChildTransform.gameObject.AddComponent<TLabVRGrabbable>();

                    // SetLayerMask
                    grabbableChild.gameObject.layer = LayerMask.NameToLayer("TLabGrabbable");

                    // Rotatable
                    grabbableChild.UseRigidbody(false, false);

                    TLabVRRotatable rotatableChild = grabbableChild.gameObject.GetComponent<TLabVRRotatable>();
                    if (rotatableChild == null) rotatableChild = grabbableChild.gameObject.AddComponent<TLabVRRotatable>();

                    // MeshCollider
                    MeshCollider meshColliderChild = grabbableChildTransform.gameObject.GetComponent<MeshCollider>();
                    if (meshColliderChild == null)
                        meshColliderChild = grabbableChildTransform.gameObject.AddComponent<MeshCollider>();
                    meshColliderChild.enabled = false;

                    EditorUtility.SetDirty(grabbableChild);
                    EditorUtility.SetDirty(rotatable);
                }
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif