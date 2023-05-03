# About
Based on [`com.unity.splines`](https://docs.unity3d.com/Packages/com.unity.splines@2.2/manual/index.html) version 2.2.1.

The original package provides linear, cubic Bézier, and Catmull-Rom splines. They are not [C^2-continuous](https://www.youtube.com/watch?v=jvPPXbo87ds).

This fork provides [Cem Yuksel's class of C^2 interpolating splines](http://www.cemyuksel.com/research/interpolating_splines/a_class_of_c2_interpolating_splines.pdf). Only the quadratic Bézier interpolator is implemented at the moment.

## Note
Only Yuksel splines are supported in this package. Support of all other types from the original package was removed to simplify the implementation. This package can be installed side by side with the original.

# Installation
* Install the package [from its git URL](https://docs.unity3d.com/Manual/upm-ui-giturl.html) or [from a local copy](https://docs.unity3d.com/Manual/upm-ui-local.html).
* It doesn't depend on the `com.unity.splines` package and won't conflict if it's present

# Possible Improvements
* Investigate C^1 and C^2 continuity for normals along the spline
* Implement circular, elliptical, and hybrid interpolators
* Shared interfaces with `com.unity.splines`
* Shader utility functions have not been reimplemented for Yuksel splines
* Automated tests have not been reimplemented for Yuksel splines
* Yuksel splines and other types of splines co-existing in one package

## Changes
### BezierKnot
This class hasn't been renamed to minimize changes relative the original package. It is however a Yuksel curve control point now:

* No more concept of tangents or rotation
* The spline doesn't necessarily pass through every control point
* Rotation is constrained to a single axis which is the tangent of the spline at the control point

### BezierCurve
This class hasn't been renamed to minimize changes relative to the original package. It is however a Yuksel curve now:

* Stores two interpolating curves and the parameters of the middle interpolating point
* Trigonometric blend between the two interpolator curves
* When a transformation matrix should be applied to the curve, the control points are pre-multiplied for caching
