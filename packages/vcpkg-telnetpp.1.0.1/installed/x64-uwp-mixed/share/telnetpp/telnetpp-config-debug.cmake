#----------------------------------------------------------------
# Generated CMake target import file for configuration "Debug".
#----------------------------------------------------------------

# Commands may need to know the format version.
set(CMAKE_IMPORT_FILE_VERSION 1)

# Import target "telnetpp" for configuration "Debug"
set_property(TARGET telnetpp APPEND PROPERTY IMPORTED_CONFIGURATIONS DEBUG)
set_target_properties(telnetpp PROPERTIES
  IMPORTED_IMPLIB_DEBUG "${_IMPORT_PREFIX}/debug/lib/telnetpp.lib"
  IMPORTED_LINK_INTERFACE_LIBRARIES_DEBUG ""
  IMPORTED_LOCATION_DEBUG "${_IMPORT_PREFIX}/debug/bin/telnetpp.dll"
  )

list(APPEND _IMPORT_CHECK_TARGETS telnetpp )
list(APPEND _IMPORT_CHECK_FILES_FOR_telnetpp "${_IMPORT_PREFIX}/debug/lib/telnetpp.lib" "${_IMPORT_PREFIX}/debug/bin/telnetpp.dll" )

# Commands beyond this point should not need to know the version.
set(CMAKE_IMPORT_FILE_VERSION)
