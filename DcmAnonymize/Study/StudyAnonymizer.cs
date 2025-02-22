using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Threading.Tasks;
using DcmAnonymize.Names;
using Dicom;
using KeyedSemaphores;

namespace DcmAnonymize.Study
{
    public class StudyAnonymizer
    {
        private readonly RandomNameGenerator _randomNameGenerator;
        private readonly ConcurrentDictionary<string, AnonymizedStudy> _anonymizedStudies = new ConcurrentDictionary<string, AnonymizedStudy>();
        private readonly Random _random = new Random();
        private int _counter = 1;

        public StudyAnonymizer(RandomNameGenerator randomNameGenerator)
        {
            _randomNameGenerator = randomNameGenerator ?? throw new ArgumentNullException(nameof(randomNameGenerator));
            _random = new Random();
        }

        public async Task AnonymizeAsync(DicomFileMetaInformation metaInfo, DicomDataset dicomDataSet)
        {
            var originalStudyInstanceUID = dicomDataSet.GetSingleValue<string>(DicomTag.StudyInstanceUID);
            var originalModality = dicomDataSet.GetValueOrDefault<string>(DicomTag.Modality, 0, null!);

            if (!_anonymizedStudies.TryGetValue(originalStudyInstanceUID, out var anonymizedStudy))
            {
                var key = $"STUDY_{originalStudyInstanceUID}";
                using (await KeyedSemaphore.LockAsync(key))
                {
                    if (!_anonymizedStudies.TryGetValue(originalStudyInstanceUID, out anonymizedStudy))
                    {
                        anonymizedStudy = new AnonymizedStudy();

                        anonymizedStudy.StudyInstanceUID = DicomUIDGenerator.GenerateDerivedFromUUID().UID;
                        anonymizedStudy.Description = "A wonderful study";
                        anonymizedStudy.AccessionNumber = $"{originalModality}{DateTime.Now:yyyyMMddHHmm}{_counter++}";
                        anonymizedStudy.StudyRequestingPhysician = _randomNameGenerator.GenerateRandomName();
                        anonymizedStudy.StudyDateTime = DateTime.Now;

                        anonymizedStudy.StudyID = anonymizedStudy.AccessionNumber;
                        anonymizedStudy.InstitutionName = "Random Hospital " + _random.Next(1, 100);
                        anonymizedStudy.StudyPerformingPhysician = _randomNameGenerator.GenerateRandomName();
                        _anonymizedStudies[originalStudyInstanceUID] = anonymizedStudy;
                    }
                }
            }

            dicomDataSet.AddOrUpdate(DicomTag.StudyInstanceUID, anonymizedStudy.StudyInstanceUID);

            dicomDataSet.AddOrUpdate(DicomTag.AccessionNumber, anonymizedStudy.AccessionNumber);
            dicomDataSet.AddOrUpdate(new DicomPersonName(
                    DicomTag.RequestingPhysician,
                    anonymizedStudy.StudyRequestingPhysician.LastName,
                    anonymizedStudy.StudyRequestingPhysician.FirstName
                )
            );
            dicomDataSet.AddOrUpdate(new DicomPersonName(
                    DicomTag.PerformingPhysicianName,
                    anonymizedStudy.StudyPerformingPhysician.LastName,
                    anonymizedStudy.StudyPerformingPhysician.FirstName
                )
            );
            dicomDataSet.AddOrUpdate(new DicomPersonName(
                DicomTag.NameOfPhysiciansReadingStudy, 
                anonymizedStudy.StudyPerformingPhysician.LastName,
                anonymizedStudy.StudyPerformingPhysician.FirstName
                )
            );

            dicomDataSet.AddOrUpdate(DicomTag.StudyDate, anonymizedStudy.StudyDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
            dicomDataSet.AddOrUpdate(DicomTag.AcquisitionDate, anonymizedStudy.StudyDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
            dicomDataSet.AddOrUpdate(DicomTag.ContentDate, anonymizedStudy.StudyDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture));

            // Assuming the PatientAnonymizer made a pass first, we can be sure to have a patient DOB
            var patientBirthDate = dicomDataSet.GetSingleValue<DateTime>(DicomTag.PatientBirthDate);
            var studyDate = anonymizedStudy.StudyDateTime.Date;
            var patientAge = studyDate.Year - patientBirthDate.Year;
            if (patientBirthDate.Date > studyDate.AddYears(-patientAge))
            {
                patientAge--;
            }
            dicomDataSet.AddOrUpdate(DicomTag.PatientAge, $"{patientAge.ToString("000", CultureInfo.InvariantCulture)}Y");

            dicomDataSet.AddOrUpdate(DicomTag.StudyTime, anonymizedStudy.StudyDateTime.ToString("HHmmss", CultureInfo.InvariantCulture));
            dicomDataSet.AddOrUpdate(DicomTag.AcquisitionTime, anonymizedStudy.StudyDateTime.ToString("HHmmss", CultureInfo.InvariantCulture));
            dicomDataSet.AddOrUpdate(DicomTag.ContentTime, anonymizedStudy.StudyDateTime.ToString("HHmmss", CultureInfo.InvariantCulture));
            dicomDataSet.AddOrUpdate(DicomTag.StudyID, anonymizedStudy.StudyID);

            //AdditionalTags

            dicomDataSet.AddOrUpdate(DicomTag.InstitutionName, anonymizedStudy.InstitutionName);
            dicomDataSet.Remove(DicomTag.InstitutionAddress);
            dicomDataSet.AddOrUpdate(new DicomPersonName(
                    DicomTag.ReferringPhysicianName,
                    anonymizedStudy.StudyRequestingPhysician.LastName,
                    anonymizedStudy.StudyRequestingPhysician.FirstName
                )
            );
            dicomDataSet.Remove(DicomTag.PhysiciansOfRecord);
        }
    }
}