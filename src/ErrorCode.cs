namespace UmbPack
{
    /// <summary>
    /// Error codes returned by the program.
    /// </summary>
    /// <remarks>
    /// https://docs.microsoft.com/en-gb/windows/win32/debug/system-error-codes--0-499-?redirectedfrom=MSDN.
    /// </remarks>
    internal enum ErrorCode : int
    {
        /// <summary>
        /// The operation completed successfully.
        /// </summary>
        Success = 0,
        /// <summary>
        /// Incorrect function.
        /// </summary>
        InvalidFunction = 1,
        /// <summary>
        /// The system cannot find the file specified.
        /// </summary>
        FileNotFound = 2,
        /// <summary>
        /// Access is denied.
        /// </summary>
        AccessDenied = 5,
        /// <summary>
        /// The file exists.
        /// </summary>
        FileExists = 80,
        /// <summary>
        /// The filename, directory name, or volume label syntax is incorrect.
        /// </summary>
        InvalidName = 123,
        /// <summary>
        /// The file type being saved or retrieved has been blocked.
        /// </summary>
        BadFileType = 222,
    }
}
