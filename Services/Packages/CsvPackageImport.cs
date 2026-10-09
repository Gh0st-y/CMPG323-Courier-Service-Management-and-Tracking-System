using System;
using System.Collections.Generic;
using System.Linq;
using CourierService.Domain.Entities;

namespace CourierService.Services.Packages
{
    /// <summary>
    /// Turns an uploaded CSV file into rows ImportController can feed to PackageRegistrationService, one at a
    /// time, so a bulk import goes through exactly the same checks as a package entered by hand (FR-08, IR-009).
    /// </summary>
    public static class CsvPackageImport
    {
        // The columns a row must have to be registered. Names are matched trimmed and case-insensitive, so
        // "recipientfullname" or " RecipientFullName " are both fine, but a renamed or missing column isn't (IR-010).
        public static readonly string[] RequiredColumns =
        {
            "RecipientFullName",
            "RecipientIdentifierNo",
            "RecipientEmail",
            "RecipientPhone",
            "SenderName",
            "PackageType",
            "Classification",
            "StorageLocation"
        };

        // RecipientDepartment and Notes are also read when present, but a row can be registered without them,
        // so they aren't checked here.

        /// <summary>
        /// Splits CSV text into a header row and the data rows below it. False only when the file has no content
        /// at all (no header row to check).
        /// </summary>
        public static bool TryParse(string csvText, out string[] header, out List<string[]> dataRows)
        {
            header = null;
            dataRows = new List<string[]>();

            var allRows = ParseRows(csvText);

            if (allRows.Count == 0)
            {
                return false;
            }

            header = allRows[0];
            dataRows = allRows.Skip(1).ToList();
            return true;
        }

        /// <summary>
        /// Checks the header against <see cref="RequiredColumns"/>. On success, returns a lookup from column name
        /// to its position in each row, for every required and optional column that's present.
        /// </summary>
        public static bool TryMapHeader(string[] header, out Dictionary<string, int> columnIndex, out string missingColumns)
        {
            columnIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < header.Length; i++)
            {
                var name = (header[i] ?? string.Empty).Trim();
                if (name.Length > 0 && !columnIndex.ContainsKey(name))
                {
                    columnIndex[name] = i;
                }
            }

            var index = columnIndex;
            var missing = RequiredColumns.Where(c => !index.ContainsKey(c)).ToList();
            if (missing.Count > 0)
            {
                missingColumns = string.Join(", ", missing);
                return false;
            }

            missingColumns = null;
            return true;
        }

        /// <summary>
        /// Maps one data row to a request for PackageRegistrationService.Register. Only resolves the StorageLocation
        /// code to an id here (Register can't do that itself); every other field is handed over as-is and
        /// Register does the real validation (required, length, format) exactly as it does for the registration form.
        /// </summary>
        public static PackageRegistrationRequest ToRequest(
            string[] row,
            Dictionary<string, int> columnIndex,
            IEnumerable<StorageLocation> storageLocations,
            out string reason)
        {
            var locationCode = Get(row, columnIndex, "StorageLocation");
            if (locationCode == null)
            {
                reason = "Storage location is required.";
                return null;
            }

            var location = storageLocations.FirstOrDefault(l => string.Equals(l.Code, locationCode, StringComparison.OrdinalIgnoreCase));
            if (location == null)
            {
                reason = "Storage location '" + locationCode + "' was not found.";
                return null;
            }

            reason = null;
            return new PackageRegistrationRequest
            {
                RecipientFullName = Get(row, columnIndex, "RecipientFullName"),
                RecipientIdentifierNo = Get(row, columnIndex, "RecipientIdentifierNo"),
                RecipientEmail = Get(row, columnIndex, "RecipientEmail"),
                RecipientPhone = Get(row, columnIndex, "RecipientPhone"),
                RecipientDepartment = Get(row, columnIndex, "RecipientDepartment"),
                SenderName = Get(row, columnIndex, "SenderName"),
                PackageType = Get(row, columnIndex, "PackageType"),
                Classification = Get(row, columnIndex, "Classification"),
                StorageLocationId = location.StorageLocationId,
                Notes = Get(row, columnIndex, "Notes")
            };
        }

        private static string Get(string[] row, Dictionary<string, int> columnIndex, string columnName)
        {
            int index;
            if (!columnIndex.TryGetValue(columnName, out index) || index >= row.Length)
            {
                return null;
            }

            var value = row[index];
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        // A small hand-rolled RFC4180 reader: fields are comma-separated, a field can be wrapped in double quotes
        // to hold a comma or a newline, and "" inside a quoted field is one literal quote. Blank lines are skipped.
        private static List<string[]> ParseRows(string text)
        {
            var rows = new List<string[]>();
            if (string.IsNullOrEmpty(text))
            {
                return rows;
            }

            var fields = new List<string>();
            var field = new System.Text.StringBuilder();
            var inQuotes = false;
            var rowHasContent = false;

            Action endField = () =>
            {
                fields.Add(field.ToString());
                field.Clear();
            };

            Action<bool> endRow = (hadAnyContent) =>
            {
                endField();
                if (hadAnyContent || fields.Any(f => f.Length > 0))
                {
                    rows.Add(fields.ToArray());
                }
                fields.Clear();
            };

            var i = 0;
            while (i < text.Length)
            {
                var c = text[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            field.Append('"');
                            i += 2;
                            continue;
                        }

                        inQuotes = false;
                        i++;
                        continue;
                    }

                    field.Append(c);
                    i++;
                    continue;
                }

                if (c == '"')
                {
                    inQuotes = true;
                    rowHasContent = true;
                    i++;
                    continue;
                }

                if (c == ',')
                {
                    rowHasContent = true;
                    endField();
                    i++;
                    continue;
                }

                if (c == '\r')
                {
                    i++;
                    continue;
                }

                if (c == '\n')
                {
                    endRow(rowHasContent);
                    rowHasContent = false;
                    i++;
                    continue;
                }

                rowHasContent = true;
                field.Append(c);
                i++;
            }

            // The last line, if the file doesn't end with a newline
            if (field.Length > 0 || fields.Count > 0 || rowHasContent)
            {
                endRow(rowHasContent);
            }

            return rows;
        }
    }
}