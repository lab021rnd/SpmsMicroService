/* ----------------------------------------------------------------------------
 * This file was automatically generated by SWIG (http://www.swig.org).
 * Version 3.0.2
 *
 * Do not make changes to this file unless you know what you are doing--modify
 * the SWIG interface file instead.
 * ----------------------------------------------------------------------------- */

namespace Grib.Api.Interop.SWIG {

public class GribUtilGridSpec2 : global::System.IDisposable {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  protected bool swigCMemOwn;

  internal GribUtilGridSpec2(global::System.IntPtr cPtr, bool cMemoryOwn) {
    swigCMemOwn = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(GribUtilGridSpec2 obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  ~GribUtilGridSpec2() {
    Dispose();
  }

  public virtual void Dispose() {
    lock(this) {
      if (swigCPtr.Handle != global::System.IntPtr.Zero) {
        if (swigCMemOwn) {
          swigCMemOwn = false;
          GribApiProxyPINVOKE.delete_GribUtilGridSpec2(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
      global::System.GC.SuppressFinalize(this);
    }
  }

  public int gridType {
	set
	{
		GribApiProxyPINVOKE.GribUtilGridSpec2_gridType_set(swigCPtr, value);
	} 
	get
	{
		return GribApiProxyPINVOKE.GribUtilGridSpec2_gridType_get(swigCPtr);
	} 
  }

  public string gridName {
    set {
      GribApiProxyPINVOKE.GribUtilGridSpec2_gridName_set(swigCPtr, value);
    } 
    get {
      string ret = GribApiProxyPINVOKE.GribUtilGridSpec2_gridName_get(swigCPtr);
      return ret;
    } 
  }

  public int ni {
    set {
      GribApiProxyPINVOKE.GribUtilGridSpec2_ni_set(swigCPtr, value);
    } 
    get {
      int ret = GribApiProxyPINVOKE.GribUtilGridSpec2_ni_get(swigCPtr);
      return ret;
    } 
  }

  public int nj {
    set {
      GribApiProxyPINVOKE.GribUtilGridSpec2_nj_set(swigCPtr, value);
    } 
    get {
      int ret = GribApiProxyPINVOKE.GribUtilGridSpec2_nj_get(swigCPtr);
      return ret;
    } 
  }

  public double iDirectionIncrementInDegrees {
    set {
      GribApiProxyPINVOKE.GribUtilGridSpec2_iDirectionIncrementInDegrees_set(swigCPtr, value);
    } 
    get {
      double ret = GribApiProxyPINVOKE.GribUtilGridSpec2_iDirectionIncrementInDegrees_get(swigCPtr);
      return ret;
    } 
  }

  public double jDirectionIncrementInDegrees {
    set {
      GribApiProxyPINVOKE.GribUtilGridSpec2_jDirectionIncrementInDegrees_set(swigCPtr, value);
    } 
    get {
      double ret = GribApiProxyPINVOKE.GribUtilGridSpec2_jDirectionIncrementInDegrees_get(swigCPtr);
      return ret;
    } 
  }

  public double longitudeOfFirstGridPointInDegrees {
    set {
      GribApiProxyPINVOKE.GribUtilGridSpec2_longitudeOfFirstGridPointInDegrees_set(swigCPtr, value);
    } 
    get {
      double ret = GribApiProxyPINVOKE.GribUtilGridSpec2_longitudeOfFirstGridPointInDegrees_get(swigCPtr);
      return ret;
    } 
  }

  public double longitudeOfLastGridPointInDegrees {
    set {
      GribApiProxyPINVOKE.GribUtilGridSpec2_longitudeOfLastGridPointInDegrees_set(swigCPtr, value);
    } 
    get {
      double ret = GribApiProxyPINVOKE.GribUtilGridSpec2_longitudeOfLastGridPointInDegrees_get(swigCPtr);
      return ret;
    } 
  }

  public double latitudeOfFirstGridPointInDegrees {
    set {
      GribApiProxyPINVOKE.GribUtilGridSpec2_latitudeOfFirstGridPointInDegrees_set(swigCPtr, value);
    } 
    get {
      double ret = GribApiProxyPINVOKE.GribUtilGridSpec2_latitudeOfFirstGridPointInDegrees_get(swigCPtr);
      return ret;
    } 
  }

  public double latitudeOfLastGridPointInDegrees {
    set {
      GribApiProxyPINVOKE.GribUtilGridSpec2_latitudeOfLastGridPointInDegrees_set(swigCPtr, value);
    } 
    get {
      double ret = GribApiProxyPINVOKE.GribUtilGridSpec2_latitudeOfLastGridPointInDegrees_get(swigCPtr);
      return ret;
    } 
  }

  public int uvRelativeToGrid {
    set {
      GribApiProxyPINVOKE.GribUtilGridSpec2_uvRelativeToGrid_set(swigCPtr, value);
    } 
    get {
      int ret = GribApiProxyPINVOKE.GribUtilGridSpec2_uvRelativeToGrid_get(swigCPtr);
      return ret;
    } 
  }

  public double latitudeOfSouthernPoleInDegrees {
    set {
      GribApiProxyPINVOKE.GribUtilGridSpec2_latitudeOfSouthernPoleInDegrees_set(swigCPtr, value);
    } 
    get {
      double ret = GribApiProxyPINVOKE.GribUtilGridSpec2_latitudeOfSouthernPoleInDegrees_get(swigCPtr);
      return ret;
    } 
  }

  public double longitudeOfSouthernPoleInDegrees {
    set {
      GribApiProxyPINVOKE.GribUtilGridSpec2_longitudeOfSouthernPoleInDegrees_set(swigCPtr, value);
    } 
    get {
      double ret = GribApiProxyPINVOKE.GribUtilGridSpec2_longitudeOfSouthernPoleInDegrees_get(swigCPtr);
      return ret;
    } 
  }

  public double angleOfRotationInDegrees {
    set {
      GribApiProxyPINVOKE.GribUtilGridSpec2_angleOfRotationInDegrees_set(swigCPtr, value);
    } 
    get {
      double ret = GribApiProxyPINVOKE.GribUtilGridSpec2_angleOfRotationInDegrees_get(swigCPtr);
      return ret;
    } 
  }

  public int iScansNegatively {
    set {
      GribApiProxyPINVOKE.GribUtilGridSpec2_iScansNegatively_set(swigCPtr, value);
    } 
    get {
      int ret = GribApiProxyPINVOKE.GribUtilGridSpec2_iScansNegatively_get(swigCPtr);
      return ret;
    } 
  }

  public int jScansPositively {
    set {
      GribApiProxyPINVOKE.GribUtilGridSpec2_jScansPositively_set(swigCPtr, value);
    } 
    get {
      int ret = GribApiProxyPINVOKE.GribUtilGridSpec2_jScansPositively_get(swigCPtr);
      return ret;
    } 
  }

  public int n {
    set {
      GribApiProxyPINVOKE.GribUtilGridSpec2_n_set(swigCPtr, value);
    } 
    get {
      int ret = GribApiProxyPINVOKE.GribUtilGridSpec2_n_get(swigCPtr);
      return ret;
    } 
  }

  public int bitmapPresent {
    set {
      GribApiProxyPINVOKE.GribUtilGridSpec2_bitmapPresent_set(swigCPtr, value);
    } 
    get {
      int ret = GribApiProxyPINVOKE.GribUtilGridSpec2_bitmapPresent_get(swigCPtr);
      return ret;
    } 
  }

  public double missingValue {
    set {
      GribApiProxyPINVOKE.GribUtilGridSpec2_missingValue_set(swigCPtr, value);
    } 
    get {
      double ret = GribApiProxyPINVOKE.GribUtilGridSpec2_missingValue_get(swigCPtr);
      return ret;
    } 
  }

  public int[] pl {
    set {
      GribApiProxyPINVOKE.GribUtilGridSpec2_pl_set(swigCPtr, value);
    } 
	get
	{
		return GribApiProxyPINVOKE.GribUtilGridSpec2_pl_get(swigCPtr);
	} 
  }

  public int plSize {
    set {
      GribApiProxyPINVOKE.GribUtilGridSpec2_plSize_set(swigCPtr, value);
    } 
    get {
      int ret = GribApiProxyPINVOKE.GribUtilGridSpec2_plSize_get(swigCPtr);
      return ret;
    } 
  }

  public int truncation {
    set {
      GribApiProxyPINVOKE.GribUtilGridSpec2_truncation_set(swigCPtr, value);
    } 
    get {
      int ret = GribApiProxyPINVOKE.GribUtilGridSpec2_truncation_get(swigCPtr);
      return ret;
    } 
  }

  public double orientationOfTheGridInDegrees {
    set {
      GribApiProxyPINVOKE.GribUtilGridSpec2_orientationOfTheGridInDegrees_set(swigCPtr, value);
    } 
    get {
      double ret = GribApiProxyPINVOKE.GribUtilGridSpec2_orientationOfTheGridInDegrees_get(swigCPtr);
      return ret;
    } 
  }

  public int dyInMetres {
    set {
      GribApiProxyPINVOKE.GribUtilGridSpec2_dyInMetres_set(swigCPtr, value);
    } 
    get {
      int ret = GribApiProxyPINVOKE.GribUtilGridSpec2_dyInMetres_get(swigCPtr);
      return ret;
    } 
  }

  public int dxInMetres {
    set {
      GribApiProxyPINVOKE.GribUtilGridSpec2_dxInMetres_set(swigCPtr, value);
    } 
    get {
      int ret = GribApiProxyPINVOKE.GribUtilGridSpec2_dxInMetres_get(swigCPtr);
      return ret;
    } 
  }

  public GribUtilGridSpec2() : this(GribApiProxyPINVOKE.new_GribUtilGridSpec2(), true) {
  }

}

}
