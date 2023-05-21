using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Unity.Mathematics;
using UObject = UnityEngine.Object;

namespace UnityEngine.YukselSplines
{
    /// <summary>
    /// The Spline class is a collection of <see cref="BezierKnot"/>, the closed/open state, and editing representation.
    /// </summary>
    [Serializable]
    public class Spline : ISpline, IList<BezierKnot>
    {
        const int k_BatchModification = -1;

        [Serializable]
        sealed class MetaData
        {
            public DistanceToInterpolation[] DistanceToInterpolation = new DistanceToInterpolation[k_CurveDistanceLutResolution];

            public MetaData()
            {
                InvalidateCache();
            }

            public MetaData(MetaData toCopy)
            {
                Array.Copy(toCopy.DistanceToInterpolation, DistanceToInterpolation, DistanceToInterpolation.Length);
            }

            public void InvalidateCache()
            {
                DistanceToInterpolation[0] = YukselSplines.DistanceToInterpolation.Invalid;
            }
        }

        const int k_CurveDistanceLutResolution = 30;

        [SerializeField]
        List<BezierKnot> m_Knots = new List<BezierKnot>();

        float m_Length = -1f;

        [SerializeField, HideInInspector]
        List<MetaData> m_MetaData = new List<MetaData>();

        [SerializeField]
        bool m_Closed;

        [SerializeField]
        SplineDataDictionary<int> m_IntData = new SplineDataDictionary<int>();

        [SerializeField]
        SplineDataDictionary<float> m_FloatData = new SplineDataDictionary<float>();

        [SerializeField]
        SplineDataDictionary<float4> m_Float4Data = new SplineDataDictionary<float4>();

        [SerializeField]
        SplineDataDictionary<UObject> m_ObjectData = new SplineDataDictionary<UObject>();

        IEnumerable<ISplineModificationHandler> embeddedSplineData
        {
            get
            {
                foreach (var data in m_IntData) yield return data.Value;
                foreach (var data in m_FloatData) yield return data.Value;
                foreach (var data in m_Float4Data) yield return data.Value;
                foreach (var data in m_ObjectData) yield return data.Value;
            }
        }

        /// <summary>
        /// Retrieve a <see cref="SplineData{T}"/> reference for <param name="key"></param> if it exists.
        /// Note that this is a reference to the stored <see cref="SplineData{T}"/>, not a copy. Any modifications to
        /// this collection will affect the <see cref="Spline"/> data.
        /// </summary>
        /// <param name="key">The string key value to search for. Only one instance of a key value can exist in an
        /// embedded <see cref="SplineData{T}"/> collection, however keys are unique to each data type. The same key
        /// can be re-used to store float data and Object data.</param>
        /// <param name="data">The output <see cref="SplineData{T}"/> if the key is found.</param>
        /// <returns>True if the key and type combination are found, otherwise false.</returns>
        public bool TryGetFloatData(string key, out SplineData<float> data) => m_FloatData.TryGetValue(key, out data);

        /// <inheritdoc cref="TryGetFloatData"/>
        public bool TryGetFloat4Data(string key, out SplineData<float4> data) => m_Float4Data.TryGetValue(key, out data);

        /// <inheritdoc cref="TryGetFloatData"/>
        public bool TryGetIntData(string key, out SplineData<int> data) => m_IntData.TryGetValue(key, out data);

        /// <inheritdoc cref="TryGetFloatData"/>
        public bool TryGetObjectData(string key, out SplineData<UObject> data) => m_ObjectData.TryGetValue(key, out data);

        /// <summary>
        /// Returns a <see cref="SplineData{T}"/> for <paramref name="key"/>. If an instance matching the key and
        /// type does not exist, a new entry is appended to the internal collection and returned.
        /// Note that this is a reference to the stored <see cref="SplineData{T}"/>, not a copy. Any modifications to
        /// this collection will affect the <see cref="Spline"/> data.
        /// </summary>
        /// <param name="key">The string key value to search for. Only one instance of a key value can exist in an
        /// embedded <see cref="SplineData{T}"/> collection, however keys are unique to each data type. The same key
        /// can be re-used to store float data and Object data.</param>
        /// <returns>A <see cref="SplineData{T}"/> of the requested type.</returns>
        public SplineData<float> GetOrCreateFloatData(string key) => m_FloatData.GetOrCreate(key);

        /// <inheritdoc cref="GetOrCreateFloatData"/>
        public SplineData<float4> GetOrCreateFloat4Data(string key) => m_Float4Data.GetOrCreate(key);

        /// <inheritdoc cref="GetOrCreateFloatData"/>
        public SplineData<int> GetOrCreateIntData(string key) => m_IntData.GetOrCreate(key);

        /// <inheritdoc cref="GetOrCreateFloatData"/>
        public SplineData<UObject> GetOrCreateObjectData(string key) => m_ObjectData.GetOrCreate(key);

        /// <summary>
        /// Remove a <see cref="SplineData{T}"/> value.
        /// </summary>
        /// <param name="key">The string key value to search for. Only one instance of a key value can exist in an
        /// embedded <see cref="SplineData{T}"/> collection, however keys are unique to each data type. The same key
        /// can be re-used to store float data and Object data.</param>
        /// <returns>Returns true if a matching <see cref="SplineData{T}"/> key value pair was found and removed, or
        /// false if no match was found.</returns>
        public bool RemoveFloatData(string key) => m_FloatData.Remove(key);

        /// <inheritdoc cref="RemoveFloatData"/>
        public bool RemoveFloat4Data(string key) => m_Float4Data.Remove(key);

        /// <inheritdoc cref="RemoveFloatData"/>
        public bool RemoveIntData(string key) => m_IntData.Remove(key);

        /// <inheritdoc cref="RemoveFloatData"/>
        public bool RemoveObjectData(string key) => m_ObjectData.Remove(key);

        /// <summary>
        /// Get a collection of the keys of embedded <see cref="SplineData{T}"/> for this type.
        /// </summary>
        /// <returns>An enumerable list of keys present for the requested type.</returns>
        public IEnumerable<string> GetFloatDataKeys() => m_FloatData.Keys;

        /// <inheritdoc cref="GetFloatDataKeys"/>
        public IEnumerable<string> GetFloat4DataKeys() => m_Float4Data.Keys;

        /// <inheritdoc cref="GetFloatDataKeys"/>
        public IEnumerable<string> GetIntDataKeys() => m_IntData.Keys;

        /// <inheritdoc cref="GetFloatDataKeys"/>
        public IEnumerable<string> GetObjectDataKeys() => m_ObjectData.Keys;

        /// <inheritdoc cref="GetFloatDataKeys"/>
        public IEnumerable<string> GetSplineDataKeys(EmbeddedSplineDataType type)
        {
            switch (type)
            {
                case EmbeddedSplineDataType.Float: return m_FloatData.Keys;
                case EmbeddedSplineDataType.Float4: return m_Float4Data.Keys;
                case EmbeddedSplineDataType.Int: return m_IntData.Keys;
                case EmbeddedSplineDataType.Object: return m_ObjectData.Keys;
                default: throw new InvalidEnumArgumentException();
            }
        }

        /// <summary>
        /// Get a collection of the <see cref="SplineData{T}"/> values for this type.
        /// </summary>
        /// <returns>An enumerable list of values present for the requested type.</returns>
        public IEnumerable<SplineData<float>> GetFloatDataValues() => m_FloatData.Values;

        /// <inheritdoc cref="GetFloatDataValues"/>
        public IEnumerable<SplineData<float4>> GetFloat4DataValues() => m_Float4Data.Values;

        /// <inheritdoc cref="GetFloatDataValues"/>
        public IEnumerable<SplineData<int>> GetIntDataValues() => m_IntData.Values;

        /// <inheritdoc cref="GetFloatDataValues"/>
        public IEnumerable<SplineData<Object>> GetObjectDataValues() => m_ObjectData.Values;

        /// <summary>
        /// Set the <see cref="SplineData{T}"/> for <param name="key"></param>.
        /// </summary>
        /// <param name="key">The string key value to search for. Only one instance of a key value can exist in an
        /// embedded <see cref="SplineData{T}"/> collection, however keys are unique to each data type. The same key
        /// can be re-used to store float data and Object data.</param>
        /// <param name="value">The <see cref="SplineData{T}"/> to set. This value will be copied.</param>
        public void SetFloatData(string key, SplineData<float> value) => m_FloatData[key] = value;

        /// <inheritdoc cref="SetFloatData"/>
        public void SetFloat4Data(string key, SplineData<float4> value) => m_Float4Data[key] = value;

        /// <inheritdoc cref="SetFloatData"/>
        public void SetIntData(string key, SplineData<int> value) => m_IntData[key] = value;

        /// <inheritdoc cref="SetFloatData"/>
        public void SetObjectData(string key, SplineData<UObject> value) => m_ObjectData[key] = value;

        /// <summary>
        /// Return the number of knots.
        /// </summary>
        public int Count => m_Knots.Count;

        /// <summary>
        /// Returns true if this Spline is read-only, false if it is mutable.
        /// </summary>
        public bool IsReadOnly => false;

        /// <summary>
        /// Invoked in the editor any time a spline property is modified.
        /// </summary>
        /// <remarks>
        /// In the editor this can be invoked many times per-frame.
        /// Prefer to use <see cref="UnityEditor.YukselSplines.EditorSplineUtility.AfterSplineWasModified"/> when
        /// working with splines in the editor.
        /// </remarks>
        [Obsolete("Deprecated, use " + nameof(Changed) + " instead.")]
        public event Action changed;

        /// <summary>
        /// Invoked any time a spline is modified.
        /// </summary>
        /// <remarks>
        /// First parameter is the target Spline that the event is raised for, second parameter is
        /// the knot index and the third parameter represents the type of change that occurred.
        /// If the event does not target a specific knot, the second parameter will have the value of -1.
        ///
        /// In the editor this callback can be invoked many times per-frame.
        /// Prefer to use <see cref="UnityEditor.YukselSplines.EditorSplineUtility.AfterSplineWasModified"/> when
        /// working with splines in the editor.
        /// </remarks>
        /// <seealso cref="SplineModification"/>
        public static event Action<Spline, int, SplineModification> Changed;

#if UNITY_EDITOR
        internal static Action<Spline> afterSplineWasModified;
        [NonSerialized]
        bool m_QueueAfterSplineModifiedCallback;
#endif
        
        (float curve0, float curve1) m_LastKnotChangeCurveLengths;

        internal void SetDirtyNoNotify()
        {
            EnsureMetaDataValid();
            m_Length = -1f;
            for (int i = 0, c = m_MetaData.Count; i < c; ++i)
                m_MetaData[i].InvalidateCache();
        }

        internal void SetDirty(SplineModification modificationEvent, int knotIndex = k_BatchModification)
        {
            SetDirtyNoNotify();

#pragma warning disable 618
            changed?.Invoke();
#pragma warning restore 618

            OnSplineChanged();

            foreach (var data in embeddedSplineData)
                data.OnSplineModified(new SplineModificationData(this, modificationEvent, knotIndex, m_LastKnotChangeCurveLengths.curve0, m_LastKnotChangeCurveLengths.curve1));

            Changed?.Invoke(this, knotIndex, modificationEvent);

#if UNITY_EDITOR
            if (m_QueueAfterSplineModifiedCallback)
                return;

            m_QueueAfterSplineModifiedCallback = true;

            UnityEditor.EditorApplication.delayCall += () =>
            {
                afterSplineWasModified?.Invoke(this);
                m_QueueAfterSplineModifiedCallback = false;
            };
#endif
        }

        /// <summary>
        /// Invoked any time a spline property is modified.
        /// </summary>
        /// <remarks>
        /// In the editor this can be invoked many times per-frame.
        /// Prefer to use <see cref="UnityEditor.YukselSplines.EditorSplineUtility.AfterSplineWasModified"/> when working
        /// with splines in the editor.
        /// </remarks>
        protected virtual void OnSplineChanged()
        {
        }

        void EnsureMetaDataValid()
        {
            while(m_MetaData.Count < m_Knots.Count)
                m_MetaData.Add(new MetaData());
        }

        /// <summary>
        /// A collection of <see cref="BezierKnot"/>.
        /// </summary>
        public IEnumerable<BezierKnot> Knots
        {
            get => m_Knots;
            set
            {
                m_Knots = new List<BezierKnot>(value);
                m_MetaData = new List<MetaData>(m_Knots.Count);
                SetDirty(SplineModification.Default);
            }
        }

        /// <summary>
        /// Whether the spline is open (has a start and end point) or closed (forms an unbroken loop).
        /// </summary>
        public bool Closed
        {
            get => m_Closed;
            set
            {
                if (m_Closed == value)
                    return;
                m_Closed = value;
                SetDirty(SplineModification.ClosedModified);
            }
        }

        /// <summary>
        /// Return the first index of an element matching item.
        /// </summary>
        /// <param name="item">The knot to locate.</param>
        /// <returns>The zero-based index of the knot, or -1 if not found.</returns>
        public int IndexOf(BezierKnot item) => m_Knots.IndexOf(item);

        /// <summary>
        /// Adds a <see cref="BezierKnot"/> at the specified <paramref name="index"/>.
        /// </summary>
        /// <param name="index">The zero-based index to insert the new element.</param>
        /// <param name="knot">The <see cref="BezierKnot"/> to insert.</param>
        /// </param>
        public void Insert(int index, BezierKnot knot)
        {
            EnsureMetaDataValid();
            CacheKnotOperationCurves(index);
            m_Knots.Insert(index, knot);
            m_MetaData.Insert(index, new MetaData());
            SetDirty(SplineModification.KnotInserted, index);
        }

        /// <summary>
        /// Creates a <see cref="BezierKnot"/> at the specified <paramref name="index"/> with a <paramref name="curveT"/> normalized offset.
        /// </summary>
        /// <param name="index">The zero-based index to insert the new element at.</param>
        /// <param name="curveT">The normalized offset along the curve.</param>
        /// </param>
        internal void InsertOnCurve(int index, float curveT)
        {
            var previousIndex = SplineUtility.PreviousIndex(index, Count, Closed);

            var curveToSplit = GetCurve(previousIndex);

            var t = math.clamp(curveT, 0f, 1f);
            var newPoint = new BezierKnot(curveToSplit.EvaluatePosition(t));

            Insert(index, newPoint);
        }

        /// <summary>
        /// Removes the knot at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the element to remove.</param>
        public void RemoveAt(int index)
        {
            EnsureMetaDataValid();
            CacheKnotOperationCurves(index);
            m_Knots.RemoveAt(index);
            m_MetaData.RemoveAt(index);
            var next = Mathf.Clamp(index, 0, Count-1);

            SetDirty(SplineModification.KnotRemoved, index);
        }

        /// <summary>
        /// Get or set the knot at <paramref name="index"/>.
        /// </summary>
        /// <param name="index">The zero-based index of the element to get or set.</param>
        public BezierKnot this[int index]
        {
            get => m_Knots[index];
            set => SetKnot(index, value);
        }

        /// <summary>
        /// Sets the value of a knot at index.
        /// </summary>
        /// <param name="index">The index of the <see cref="BezierKnot"/> to set.</param>
        /// <param name="value">The <see cref="BezierKnot"/> to set.</param>
        /// <param name="main">The tangent to prioritize if the tangents are modified to conform with the
        public void SetKnot(int index, BezierKnot value)
        {
            CacheKnotOperationCurves(index);
            SetKnotNoNotify(index, value);
            SetDirty(SplineModification.KnotModified, index);
        }

        /// <summary>
        /// Sets the value of a knot index without invoking any change callbacks.
        /// </summary>
        /// <param name="index">The index of the <see cref="BezierKnot"/> to set.</param>
        /// <param name="value">The <see cref="BezierKnot"/> to set.</param>
        /// <param name="main">The tangent to prioritize if the tangents are modified to conform with the
        public void SetKnotNoNotify(int index, BezierKnot value)
        {
            m_Knots[index] = value;
        }

        /// <summary>
        /// Default constructor creates a spline with no knots, not closed.
        /// </summary>
        public Spline() { }

        /// <summary>
        /// Create a spline with a pre-allocated knot capacity.
        /// </summary>
        /// <param name="knotCapacity">The capacity of the knot collection.</param>
        /// <param name="closed">Whether the spline is open (has a start and end point) or closed (forms an unbroken loop).</param>
        public Spline(int knotCapacity, bool closed = false)
        {
            m_Knots = new List<BezierKnot>(knotCapacity);
            m_Closed = closed;
        }

        /// <summary>
        /// Create a spline from a collection of <see cref="BezierKnot"/>.
        /// </summary>
        /// <param name="knots">A collection of <see cref="BezierKnot"/>.</param>
        /// <param name="closed">Whether the spline is open (has a start and end point) or closed (forms an unbroken loop).</param>
        public Spline(IEnumerable<BezierKnot> knots, bool closed = false)
        {
            m_Knots = knots.ToList();
            m_Closed = closed;
        }
        
        /// <summary>
        /// Create a copy of a spline.
        /// </summary>
        /// <param name="spline">The spline to copy in that new instance.</param>
        public Spline(Spline spline)
        {
            m_Knots = spline.Knots.ToList();
            m_Closed = spline.Closed;

            //Deep copy of the 4 embedded SplineData
            foreach (var data in spline.m_IntData)
                m_IntData[data.Key] = data.Value;
            foreach (var data in spline.m_FloatData)
                m_FloatData[data.Key] = data.Value;
            foreach (var data in spline.m_Float4Data)
                m_Float4Data[data.Key] = data.Value;
            foreach (var data in spline.m_ObjectData)
                m_ObjectData[data.Key] = data.Value;
        }

        public static (int p0, int p1, int p2, int p3) GetCurveKnotIndicesForIndex(int index, int knotCount, bool closed)
        {
            int p0;
            int p1 = index;
            int p2;
            int p3;

            if (closed)
            {
                p0 = (index - 1 + knotCount) % knotCount;
                p2 = (index + 1) % knotCount;
                p3 = (index + 2) % knotCount;
            }
            else
            {
                p0 = math.max(index - 1, 0);
                p2 = math.min(index + 1, knotCount - 1);
                p3 = math.min(index + 2, knotCount - 1);
            }

            return (p0, p1, p2, p3);
        }

        /// <summary>
        /// Get a <see cref="BezierCurve"/> from a knot index.
        /// </summary>
        /// <param name="index">The knot index that serves as the first control point for this curve.</param>
        /// <returns>
        /// A <see cref="BezierCurve"/> formed by the knot at index and the next knot.
        /// </returns>
        public BezierCurve GetCurve(int index)
        {
            var knots = GetCurveKnotIndicesForIndex(index, m_Knots.Count, m_Closed);

            return new BezierCurve(m_Knots[knots.p0], m_Knots[knots.p1], m_Knots[knots.p2], m_Knots[knots.p3]);
        }

        public BezierCurve GetCurve(int index, float4x4 matrix)
        {
            var knots = GetCurveKnotIndicesForIndex(index, m_Knots.Count, m_Closed);

            return new BezierCurve(m_Knots[knots.p0], m_Knots[knots.p1], m_Knots[knots.p2], m_Knots[knots.p3], matrix);
        }

        /// <summary>
        /// Return the length of a curve.
        /// </summary>
        /// <param name="index"></param>
        /// <seealso cref="Warmup"/>
        /// <seealso cref="GetLength"/>
        /// <returns></returns>
        public float GetCurveLength(int index)
        {
            EnsureMetaDataValid();
            if(m_MetaData[index].DistanceToInterpolation[0].Distance < 0f)
                CurveUtility.CalculateCurveLengths(GetCurve(index), m_MetaData[index].DistanceToInterpolation);
            var cumulativeCurveLengths = m_MetaData[index].DistanceToInterpolation;
            return cumulativeCurveLengths.Length > 0 ? cumulativeCurveLengths[cumulativeCurveLengths.Length - 1].Distance : 0f;
        }

        /// <summary>
        /// Return the sum of all curve lengths, accounting for <see cref="Closed"/> state.
        /// Note that this value is not accounting for transform hierarchy. If you require length in world space use
        /// </summary>
        /// <remarks>
        /// This value is cached. It is recommended to call this once in a non-performance critical path to ensure that
        /// the cache is valid.
        /// </remarks>
        /// <seealso cref="Warmup"/>
        /// <seealso cref="GetCurveLength"/>
        /// <returns>
        /// Returns the sum length of all curves composing this spline, accounting for closed state.
        /// </returns>
        public float GetLength()
        {
            if (m_Length < 0f)
            {
                m_Length = 0f;
                for (int i = 0, c = Closed ? Count : Count - 1; i < c; ++i)
                    m_Length += GetCurveLength(i);
            }

            return m_Length;
        }

        DistanceToInterpolation[] GetCurveDistanceLut(int index)
        {
            if (m_MetaData[index].DistanceToInterpolation[0].Distance < 0f)
                CurveUtility.CalculateCurveLengths(GetCurve(index), m_MetaData[index].DistanceToInterpolation);

            return m_MetaData[index].DistanceToInterpolation;
        }

        /// <summary>
        /// Return the normalized interpolation (t) corresponding to a distance on a <see cref="BezierCurve"/>.
        /// </summary>
        /// <param name="curveIndex"> The zero-based index of the curve.</param>
        /// <param name="curveDistance">The curve-relative distance to convert to an interpolation ratio (also referred to as 't').</param>
        /// <returns>  The normalized interpolation ratio associated to distance on the designated curve.</returns>
        public float GetCurveInterpolation(int curveIndex, float curveDistance)
            => CurveUtility.GetDistanceToInterpolation(GetCurveDistanceLut(curveIndex), curveDistance);

        /// <summary>
        /// Ensure that all caches contain valid data. Call this to avoid unexpected performance costs when accessing
        /// spline data. Caches remain valid until any part of the spline state is modified.
        /// </summary>
        public void Warmup()
        {
            var _ = GetLength();
        }

        /// <summary>
        /// Change the size of the <see cref="BezierKnot"/> list.
        /// </summary>
        /// <param name="newSize">The new size of the knots collection.</param>
        public void Resize(int newSize)
        {
            int originalSize = Count;
            newSize = math.max(0, newSize);

            if (newSize == originalSize)
                return;

            if (newSize > originalSize)
            {
                while (m_Knots.Count < newSize)
                    Add(new BezierKnot());

            }
            else if (newSize < originalSize)
            {
                while(newSize < Count)
                    RemoveAt(Count-1);
            }
        }

        /// <summary>
        /// Create an array of spline knots.
        /// </summary>
        /// <returns>Return a new array copy of the knots collection.</returns>
        public BezierKnot[] ToArray()
        {
            return m_Knots.ToArray();
        }

        /// <summary>
        /// Copy the values from <paramref name="copyFrom"/> to this spline.
        /// </summary>
        /// <param name="copyFrom">The spline to copy property data from.</param>
        public void Copy(Spline copyFrom)
        {
            if (copyFrom == this)
                return;

            m_Closed = copyFrom.Closed;
            m_Knots.Clear();
            m_Knots.AddRange(copyFrom.m_Knots);
            m_MetaData.Clear();
            for (int i = 0; i < copyFrom.m_MetaData.Count; ++i)
                m_MetaData.Add(new MetaData(copyFrom.m_MetaData[i]));

            SetDirty(SplineModification.Default);
        }

        /// <summary>
        /// Get an enumerator that iterates through the <see cref="BezierKnot"/> collection.
        /// </summary>
        /// <returns>An IEnumerator that is used to iterate the <see cref="BezierKnot"/> collection.</returns>
        public IEnumerator<BezierKnot> GetEnumerator() => m_Knots.GetEnumerator();

        /// <summary>
        /// Gets an enumerator that iterates through the <see cref="BezierKnot"/> collection.
        /// </summary>
        /// <returns>An IEnumerator that is used to iterate the <see cref="BezierKnot"/> collection.</returns>
        IEnumerator IEnumerable.GetEnumerator() => m_Knots.GetEnumerator();

        /// <summary>
        /// Adds a knot to the spline.
        /// </summary>
        /// <param name="item">The <see cref="BezierKnot"/> to add.</param>
        /// </param>
        public void Add(BezierKnot item)
        {
            Insert(Count, item);
        }

        /// <summary>
        /// Remove all knots from the spline.
        /// </summary>
        public void Clear()
        {

            m_Knots.Clear();
            m_MetaData.Clear();
            SetDirty(SplineModification.KnotRemoved);
        }

        /// <summary>
        /// Return true if a knot is present in the spline.
        /// </summary>
        /// <param name="item">The <see cref="BezierKnot"/> to locate.</param>
        /// <returns>Returns true if the knot is found, false if it is not present.</returns>
        public bool Contains(BezierKnot item) => m_Knots.Contains(item);

        /// <summary>
        /// Copies the contents of the knot list to an array starting at an index.
        /// </summary>
        /// <param name="array">The destination array to place the copied item in.</param>
        /// <param name="arrayIndex">The zero-based index to copy.</param>
        public void CopyTo(BezierKnot[] array, int arrayIndex) => m_Knots.CopyTo(array, arrayIndex);

        /// <summary>
        /// Removes the first matching knot.
        /// </summary>
        /// <param name="item">The <see cref="BezierKnot"/> to locate and remove.</param>
        /// <returns>Returns true if a matching item was found and removed, false if no match was discovered.</returns>
        public bool Remove(BezierKnot item)
        {
            var index = m_Knots.IndexOf(item);
            if (index >= 0)
            {
                RemoveAt(index);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Remove any unused embedded <see cref="SplineData{T}"/> entries.
        /// </summary>
        /// <seealso cref="GetOrCreateFloatData"/>
        /// <seealso cref="GetOrCreateFloat4Data"/>
        /// <seealso cref="GetOrCreateIntData"/>
        /// <seealso cref="GetOrCreateObjectData"/>
        internal void RemoveUnusedSplineData()
        {
            m_FloatData.RemoveEmpty();
            m_Float4Data.RemoveEmpty();
            m_IntData.RemoveEmpty();
            m_ObjectData.RemoveEmpty();
        }
        
        internal void CacheKnotOperationCurves(int index)
        {
            if (Count <= 1)
                return;
            
            m_LastKnotChangeCurveLengths.curve0= GetCurveLength(this.PreviousIndex(index));
            if (index < Count)
                m_LastKnotChangeCurveLengths.curve1 = GetCurveLength(index);
        }
    }
}
