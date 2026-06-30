namespace AutoOcrs.Core.Enums;

public enum ProjectStatus { Active = 0, Archived = 1 }
public enum BatchStatus { Created = 0, Importing = 1, Processing = 2, OcrDone = 3, Classifying = 4, Extracting = 5, ReadyForReview = 6, Reviewing = 7, Completed = 8 }
public enum DocumentStatus { Pending = 0, Importing = 1, OcrProcessing = 2, OcrDone = 3, Classifying = 4, Classified = 5, Extracting = 6, Extracted = 7, ReadyForReview = 8, Reviewing = 9, Approved = 10, Rejected = 11, Error = 12 }
public enum FieldType { Text = 0, Number = 1, Date = 2, Select = 3, MultiSelect = 4 }
public enum ReviewStatus { Assigned = 0, InProgress = 1, Completed = 2 }
