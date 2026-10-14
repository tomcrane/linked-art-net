import re
from datetime import date, timedelta
from dateutil.parser import parse


def extract_timespan(begin_date: str, end_date: str, label: str) -> (dict, str):
    timespan = None
    begin_date = strip_prefixes(begin_date)
    end_date = strip_prefixes(end_date)

    # if we see these strings, don't attempt an initial parse
    skip_parse_list = ["[", "-", "early", "late", "mid", "century", " - ", " to ", "?"]
    if begin_date != end_date and not any(s in begin_date for s in skip_parse_list):
        # try to parse directly, no string prep
        timespan, label = _extract_timespan_internal(begin_date, end_date, label)
    if timespan is None:
        begin_date, end_date = tidy_begin_and_end_dates(begin_date, end_date)
        timespan, label = _extract_timespan_internal(begin_date, end_date, label)
    return timespan, label


def _extract_timespan_internal(begin_date: str, end_date: str, label: str):
    timespan = {
        "type": "TimeSpan",
        "label": label
    }
    valid = False
    default_date = date(1970, 1, 1)  # to avoid parsing "1969" to 1969-thismonth-thisdate
    if begin_date:
        try:
            dt = parse(begin_date, fuzzy=True, default=default_date)
            valid = True
            timespan['start'] = dt.isoformat()
            timespan['end'] = timespan['start']
        except (ValueError, TypeError):
            pass
    if end_date:
        if is_simple_year(end_date):
            end_date = str(int(end_date) + 1)
        try:
            dt = parse(end_date, fuzzy=True, default=default_date)
            valid = True
            timespan['end'] = dt.isoformat()
            if timespan.get('start', None) is None:
                timespan['start'] = timespan['end']

        except (ValueError, TypeError):
            pass

    start = timespan.get('start', None)
    end = timespan.get('end', None)
    if start and end and end > start:
        end_dt = parse(end, fuzzy=True, default=default_date) - timedelta(days=1)
        timespan['end'] = end_dt.isoformat()

    if valid:
        return timespan, label

    return None, label


def strip_prefixes(date_string: str):
    """Remove prefixes, e.g. Circa and copyright"""
    to_strip = ["c.", "c", "©"]
    if date_string:
        for s in to_strip:
            if date_string.startswith(s):
                return date_string.lstrip(s)
    return date_string


def tidy_begin_and_end_dates(begin_date: str, end_date: str) -> (str, str):
    """
    ad-hoc data cleansing for dates.
    Don't attempt to parse, just recognise patterns that we can help the parser with.
    """
    if begin_date is None:
        return begin_date, end_date

    if begin_date == end_date or (begin_date is not None and end_date is None):
        # one field supplied, or both the same
        single_date_str = begin_date.strip()
        single_date_str = single_date_str.replace("?", "")
        single_date_str = single_date_str.replace("()", "")  # "(?)" => ""

        if re.match(r'\[[0-9]+_+\]', single_date_str):
            # "[17_]", "[189_]
            relevant_parts = get_numerical_parts(single_date_str)
            if len(relevant_parts) == 1:
                single_date_str = relevant_parts[0].ljust(4, '0')
                end_date = str(int(relevant_parts[0]) + 1).ljust(4, '0')
                # now we need to knock off a year from the end_date, so the parser can add it back to make the range
                end_date = str(int(end_date) - 1)
                return single_date_str, end_date

        match = re.match(r'^\[(.+)\]$', single_date_str)
        if match:
            # remove outer [..] (but only after the first test
            # single_date_str = match.group(1) - no - doesn't handle [..] - [..] variant
            single_date_str = single_date_str.replace('[', '')
            single_date_str = single_date_str.replace(']', '')

        # is it a from-to?
        if " to " in single_date_str or " - " in single_date_str or "," in single_date_str or len(single_date_str.split("-")) == 2:
            # "1948 to early 1950's"
            parts = single_date_str.split(" to ")
            if len(parts) == 1:
                parts = single_date_str.split("-")
            if len(parts) == 1:
                parts = single_date_str.split(",")

            parts = [x.strip() for x in parts]
            if is_simple_year(parts[0]) and len(parts[1]) == 2:
                parts[1] = f"{parts[0][:2]}{parts[1]}"
            if parts[0] == "early" or parts[0] == "mid":
                # e.g., "early - mid 13th century" => "early 13th century - mid 13th century"
                parts[0] = f"{parts[0]} {parts[1][parts[1].find(' '):]}"
            candidate = get_year_range_as_single_string(parts[0], parts[1])
            if candidate:
                single_date_str = candidate
        elif "century" in single_date_str or single_date_str.endswith("s"):
            # not a range, but still 100 years..., or a decade
            single_date_str = get_year_range_as_single_string(single_date_str, single_date_str)
        elif is_simple_year(single_date_str):
            single_date_str = f"{single_date_str}-{single_date_str}"

        # it's not a simple year, but is a simple year the only numerical content of the string?
        num_parts = get_numerical_parts(single_date_str)
        if len(num_parts) == 1 and is_simple_year(num_parts[0]):
            if f"{num_parts[0]}s" in single_date_str or f"{num_parts[0]}'s" in single_date_str:
                single_date_str = get_year_range_as_single_string(single_date_str, single_date_str)
            else:
                single_date_str = f"{num_parts[0]}-{num_parts[0]}"

        # we have 2 numeric parts that are simple years e.g. "1981 (1982)"
        if len(num_parts) == 2 and is_simple_year(num_parts[0]) and is_simple_year(num_parts[1]):
            single_date_str = get_year_range_as_single_string(num_parts[0], num_parts[1])

        parts = single_date_str.split('-')
        if len(parts) == 2:
            # "yyyy-yyyy"
            if len(parts[0]) == 4 and len(parts[1]) == 2:
                parts[1] = f"{parts[0][:2]}{parts[1]}"
            return parts[0], parts[1]

    return single_date_str, end_date


def get_year_range_as_single_string(start, end):
    year_start = try_get_year(start, False)
    year_end = try_get_year(end, True)
    if year_start >= 0 and year_end >= 0:
        return f"{year_start}-{year_end}"
    if start == end:
        return start
    return None


def is_simple_year(date_string: str) -> bool:
    return re.match(r'^[0-9]{4}$', date_string.strip()) is not None


def try_get_year(year: str, is_upper_bound: bool):
    year = clean_year(year)

    # first see if this string is just a year
    if is_simple_year(year):
        return int(year)

    yyyy = -1
    if "th century" in year:
        yy = get_numerical_parts(year)[0]
        yyyy = int(yy.ljust(4, '0')) - 100  # 14th century => 1300
        if is_upper_bound:
            if "early" in year:
                yyyy = yyyy + 49
            elif "mid" in year:
                yyyy = yyyy + 74
            else:
                yyyy = yyyy + 99
        else:
            if "mid" in year:
                yyyy = yyyy + 25
            elif "late" in year:
                yyyy = yyyy + 50
        return yyyy

    # decade - "1950s" or "1950's"
    match = re.search(r'([0-9]{4})[\']*s', year)
    if match:
        yyyy = int(match.group(1))
        if is_upper_bound:
            if "early" in year:
                yyyy = yyyy + 4
            elif "mid" in year:
                yyyy = yyyy + 7
            else:
                yyyy = yyyy + 9
        else:
            if "mid" in year:
                yyyy = yyyy + 2
            elif "late" in year:
                yyyy = yyyy + 5
        return yyyy

    return yyyy


def clean_year(date_string: str):
    return strip_prefixes(date_string.strip().lstrip("[").rstrip("]"))


def get_numerical_parts(date_string: str):
    numerical_parts = re.split(r'[^0-9]', date_string)
    return [x for x in numerical_parts if len(x) >= 2]