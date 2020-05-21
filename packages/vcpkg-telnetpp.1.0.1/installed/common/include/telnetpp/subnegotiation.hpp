#pragma once

#include "telnetpp/core.hpp"
#include <iosfwd>
#include <utility>

namespace telnetpp {

//* =========================================================================
/// \brief A class that encapsulates a Telnet subnegotiation.
//* =========================================================================
class TELNETPP_EXPORT subnegotiation
{
public:
    //* =====================================================================
    /// \brief Constructor
    //* =====================================================================
#if _WIN32
    const subnegotiation(option_type option, bytes content) noexcept
#else
    constexpr subnegotiation(option_type option, bytes content) noexcept
#endif
      : option_(std::move(option)),
        content_(std::move(content))
    {
    }

    //* =====================================================================
    /// \brief Returns the option for this subnegotiation.
    //* =====================================================================
    constexpr option_type option() const noexcept
    {
        return option_;
    }

    //* =====================================================================
    /// \brief Returns the content for this subnegotiation.
    //* =====================================================================
#if _WIN32
    const bytes content() const noexcept
#else
    constexpr bytes content() const noexcept
#endif
    {
        return content_;
    }


private:
    option_type option_;
    bytes content_;
};

//* =========================================================================
/// \brief Comparison function for subnegotiations
//* =========================================================================
TELNETPP_EXPORT
constexpr bool operator==(subnegotiation const &lhs, subnegotiation const &rhs) noexcept
{
    return lhs.option() == rhs.option()
        && lhs.content() == rhs.content();
}

//* =========================================================================
/// \brief Stream output for subnegotiations
//* =========================================================================
TELNETPP_EXPORT
std::ostream &operator<<(std::ostream &out, subnegotiation const &cmd);

}
