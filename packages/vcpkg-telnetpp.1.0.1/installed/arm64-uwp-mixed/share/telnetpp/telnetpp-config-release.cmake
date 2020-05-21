#----------------------------------------------------------------
# Generated CMake target import file for configuration "Release".
#----------------------------------------------------------------

# Commands may need to know the format version.
set(CMAKE_IMPORT_FILE_VERSION 1)

# Import target "telnetpp" for configuration "Release"
set_property(TARGET telnetpp APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(telnetpp PROPERTIES
  IMPORTED_IMPLIB_RELEASE "${_IMPORT_PREFIX}/lib/telnetpp.lib"
  IMPORTED_LINK_INTERFACE_LIBRARIES_RELEASE ""
  IMPORTED_LOCATION_RELEASE "${_IMPORT_PREFIX}/bin/telnetpp.dll"
  )

list(APPEND _IMPORT_CHECK_TARGETS telnetpp )
list(APPEND _IMPORT_CHECK_FILES_FOR_telnetpp "${_IMPORT_PREFIX}/lib/telnetpp.lib" "${_IMPORT_PREFIX}/bin/telnetpp.dll" )

# Commands beyond this point should not need to know the version.
set(CMAKE_IMPORT_FILE_VERSION)
