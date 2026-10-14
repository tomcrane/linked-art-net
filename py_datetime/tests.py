from dateutil.parser import parse
from timespan_parser import extract_timespan


class TestTimeSpanParser:
    """
     Tests of string reorganisation before date parsing
    """

    def test_single_strings(self):
        test_date_strings = {
            "c.1928 - 32": ("1928-01-01", "1932-12-31"),
            "1920s-1950s": ("1920-01-01", "1959-12-31"),
            "1971": ("1971-01-01", "1971-12-31"),
            "[17_]": ("1700-01-01", "1799-12-31"),
            "[189_]": ("1890-01-01", "1899-12-31"),
            "1948 to early 1950's": ("1948-01-01", "1954-12-31"),
            "1900-1944": ("1900-01-01", "1944-12-31"),
            "1945-53": ("1945-01-01", "1953-12-31"),
            "1870s - 1920s": ("1870-01-01", "1929-12-31"),
            "late 13th century - early 14th century": ("1250-01-01", "1349-12-31"),
            "early 15th century": ("1400-01-01", "1449-12-31"),
            "c.1898": ("1898-01-01", "1898-12-31"),
            "c1543": ("1543-01-01", "1543-12-31"),
            "[late 12th century]": ("1150-01-01", "1199-12-31"),
            "[late 14th century] - [mid 15th century]": ("1350-01-01", "1474-12-31"),  # hah...
            "1950s": ("1950-01-01", "1959-12-31"),
            "early - mid 13th century": ("1200-01-01", "1274-12-31"),
            "mid - late 13th century": ("1225-01-01", "1299-12-31"),
            "1902 October 27": ("1902-10-27", "1902-10-27"),  # leave as-is...
            "[1847-1852]": ("1847-01-01", "1852-12-31"),
            "c. 1860": ("1860-01-01", "1860-12-31"),
            "1879 - 1881": ("1879-01-01", "1881-12-31"),
            "[1861(?)]": ("1861-01-01", "1861-12-31"),
            "1493 (first edition published)": ("1493-01-01", "1493-12-31"),
            "[1500] - [1520]": ("1500-01-01", "1520-12-31"),
            "mid 1920s - late 1920s": ("1922-01-01", "1929-12-31"),
            "13/11/08": ("2008-11-13", "2008-11-13"),  # pass through to parser
            "c.1878-79": ("1878-01-01", "1879-12-31"),
            "c.late 1870s - 1920s": ("1875-01-01", "1929-12-31"),
            "1980s (when photocopies made)": ("1980-01-01", "1989-12-31"),
            "(1989?)": ("1989-01-01", "1989-12-31"),
            "[1988], ©1988": ("1988-01-01", "1988-12-31"),
            "cop. 1980": ("1980-01-01", "1980-12-31"),
            "1977 (1978)": ("1977-01-01", "1978-12-31"),
            "1962, (i. e. 1967)": ("1962-01-01", "1967-12-31"),  # this date doesn't make sense
        }

        # still to do:
        # "July 2010": ("2010-07-01", "2010-07-31"),  # what do we want to do with just a month?
        # "1898 (original drawing by Lethaby) c.1900 (traced by John Brandon-Jones)": ("1898", "1900"),

        for k, v in test_date_strings.items():
            print(f"NEW TEST: {k}")
            timespan, label = extract_timespan(k, k, k)
            expected_start, expected_end = test_date_strings[k]

            assert parse(timespan['start']) == parse(expected_start)
            assert parse(timespan['end']) == parse(expected_end)


if __name__ == "__main__":
    test = TestTimeSpanParser()
    test.test_single_strings()