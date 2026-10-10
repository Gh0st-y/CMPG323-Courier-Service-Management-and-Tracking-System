# Operational configuration (T06)

Fees and notification subjects/bodies are read from `dbo.AppConfig` on each operation. Changes affect future registrations or notification attempts without a rebuild. Previously registered packages retain their saved fee. Reload an open registration page after a fee change.

| Source | Keys | When read |
| --- | --- | --- |
| Database | `Fee.Personal`, `Fee.WorkRelated` | Every registration and `GET /api/packages/fees` |
| Database | `Notification.ReadyForCollection.Subject`, `.Body`, `Notification.Collected.Subject`, `.Body` | Every notification composition |
| Web appSettings | `Smtp.Host`, `.Port`, `.FromAddress`, `.FromName`, `.UseSsl`, `.UserName`, `.Password`, `.TimeoutSeconds` | Each notification processing batch |
| Web appSettings | `Sms.Enabled` | Status service construction and notification processing |
| Web appSettings | `Sms.Provider` | Reserved; only the Stub implementation is available in this release |
| system.web | `sessionState timeout` | ASP.NET session inactivity timeout; the similarly named appSettings key is documentation only |

The database `Sms.Enabled` seed is legacy and is not read by the application. Use the Web appSetting. SMS disabled or failed does not suppress the separately queued email.

## Local SMTP settings

`Web/Web.config` now loads an optional `Web.config.Local` next to itself. That ignored file must contain an `<appSettings>` element, for example:

```xml
<appSettings>
  <add key="Smtp.Host" value="localhost" />
  <add key="Smtp.Port" value="2525" />
  <add key="Smtp.FromAddress" value="courier@demo.test" />
  <add key="Sms.Enabled" value="false" />
</appSettings>
```

External-file values override inline appSettings. This file does not override connectionStrings. Configure the deployed connection string separately in Web.config or an environment transform. Never commit passwords or real recipient data. Web.config changes can recycle the application; they do not require compiling it.

## Fee verification

On a synthetic database, read the current value and save it for restoration. Update `Fee.Personal` to `12.50`, then reload registration as intake.demo. The displayed amount and a newly saved personal package must both be R12.50. Restore the original setting afterwards. Existing packages must retain their original fee. Fees must be between 0 and 9999.99 inclusive, with no more than two decimal places.

The staff fee endpoint exposes only the two fees, not configuration credentials. The full `/api/config` management API remains unimplemented; database configuration changes currently require an administrator using SQL tools. A staff-facing configuration editor and audited updates are separate work.

## Template verification

Change a subject/body on a synthetic database, trigger ReadyForCollection, and inspect the local SMTP inbox. Restore the template. Supported placeholders are `{{RecipientName}}`, `{{PackageId}}`, `{{StorageLocation}}`, `{{CollectionTime}}`.

Merge the database seed fix (#110) before initializing a fresh database. Do not treat skipped SQL tests as verification of database setup.
