using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace PlantDraw.Core
{
    /// <summary>Transaction helpers with warning handling that keeps routing sessions alive.</summary>
    public static class Tx
    {
        public static void Run(Document document, string name, Action body)
        {
            if (document == null)
            {
                throw new InvalidOperationException("No document is available.");
            }

            if (document.IsReadOnly)
            {
                throw new InvalidOperationException("This document is read-only (worksharing or a central file in use).");
            }

            Transaction transaction = new Transaction(document, name);

            FailureHandlingOptions options = transaction.GetFailureHandlingOptions();
            options.SetFailuresPreprocessor(new WarningSink());
            options.SetClearAfterRollback(true);
            transaction.SetFailureHandlingOptions(options);

            transaction.Start();
            try
            {
                body();

                TransactionStatus status = transaction.Commit();
                if (status != TransactionStatus.Committed && status != TransactionStatus.RolledBack)
                {
                    throw new InvalidOperationException("Revit did not commit the change (" + status + ").");
                }

                if (status == TransactionStatus.RolledBack)
                {
                    throw new InvalidOperationException("Revit rolled the change back. Check the model warnings.");
                }
            }
            catch
            {
                try
                {
                    if (transaction.GetStatus() == TransactionStatus.Started)
                    {
                        transaction.RollBack();
                    }
                }
                catch
                {
                    // Nothing useful to do - the transaction is already finished.
                }

                throw;
            }
        }
    }

    /// <summary>
    /// Deletes warnings and lets errors abort. Routing between elements routinely raises
    /// "not connected" style warnings that must not stop the drawing loop.
    /// </summary>
    public sealed class WarningSink : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            IList<FailureMessageAccessor> failures = failuresAccessor.GetFailureMessages();
            if (failures == null || failures.Count == 0)
            {
                return FailureProcessingResult.Continue;
            }

            foreach (FailureMessageAccessor failure in failures)
            {
                if (failure.GetSeverity() == FailureSeverity.Warning)
                {
                    failuresAccessor.DeleteWarning(failure);
                }
            }

            return FailureProcessingResult.Continue;
        }
    }
}
