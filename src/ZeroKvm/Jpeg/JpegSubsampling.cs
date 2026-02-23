namespace ZeroKvm.Jpeg;

/// <summary>
/// Chrominance subsampling options
/// </summary>
/// <remarks>
/// When pixels are converted from RGB to YCbCr (see #TJCS_YCbCr) or from CMYK
/// to YCCK (see #TJCS_YCCK) as part of the JPEG compression process, some of
/// the Cb and Cr (chrominance) components can be discarded or averaged together
/// to produce a smaller image with little perceptible loss of image quality.
/// (The human eye is more sensitive to small changes in brightness than to
/// small changes in color.)  This is called "chrominance subsampling".
/// </remarks>
internal enum JpegSubsampling
{
    /// <summary>
    /// 4:4:4 chrominance subsampling (no chrominance subsampling)
    ///
    /// The JPEG or YUV image will contain one chrominance component for every
    /// pixel in the source image.
    /// </summary>
    S444,

    /// <summary>
    /// 4:2:2 chrominance subsampling
    ///
    /// The JPEG or YUV image will contain one chrominance component for every 2x1
    /// block of pixels in the source image.
    /// </summary>
    S422,

    /// <summary>
    /// 4:2:0 chrominance subsampling
    ///
    /// The JPEG or YUV image will contain one chrominance component for every 2x2
    /// block of pixels in the source image.
    /// </summary>
    S420,

    /// <summary>
    /// Grayscale
    ///
    /// The JPEG or YUV image will contain no chrominance components.
    /// </summary>
    Gray,

    /// <summary>
    /// 4:4:0 chrominance subsampling
    ///
    /// The JPEG or YUV image will contain one chrominance component for every 1x2
    /// block of pixels in the source image.
    ///
    /// @note 4:4:0 subsampling is not fully accelerated in libjpeg-turbo.
    /// </summary>
    S440,

    /// <summary>
    /// 4:1:1 chrominance subsampling
    ///
    /// The JPEG or YUV image will contain one chrominance component for every 4x1
    /// block of pixels in the source image.  All else being equal, a JPEG image
    /// with 4:1:1 subsampling is almost exactly the same size as a JPEG image
    /// with 4:2:0 subsampling, and in the aggregate, both subsampling methods
    /// produce approximately the same perceptual quality.  However, 4:1:1 is
    /// better able to reproduce sharp horizontal features.
    ///
    /// @note 4:1:1 subsampling is not fully accelerated in libjpeg-turbo.
    /// </summary>
    S411,

    /// <summary>
    /// 4:4:1 chrominance subsampling
    ///
    /// The JPEG or YUV image will contain one chrominance component for every 1x4
    /// block of pixels in the source image.  All else being equal, a JPEG image
    /// with 4:4:1 subsampling is almost exactly the same size as a JPEG image
    /// with 4:2:0 subsampling, and in the aggregate, both subsampling methods
    /// produce approximately the same perceptual quality.  However, 4:4:1 is
    /// better able to reproduce sharp vertical features.
    ///
    /// @note 4:4:1 subsampling is not fully accelerated in libjpeg-turbo.
    /// </summary>
    S441,

    /// <summary>
    /// Unknown subsampling
    ///
    /// The JPEG image uses an unusual type of chrominance subsampling.  Such
    /// images can be decompressed into packed-pixel images, but they cannot be
    /// - decompressed into planar YUV images,
    /// - losslessly transformed if #TJXOPT_CROP is specified and #TJXOPT_GRAY is
    /// not specified, or
    /// - partially decompressed using a cropping region.
    /// </summary>
    Unknown = -1,
}
