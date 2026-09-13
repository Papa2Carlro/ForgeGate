# No AutoMapper by Default

No AutoMapper/reflection-based mapping as default architecture. Important semantic transformations must remain visible and debuggable. For tiny pure value conversions, small pure/static mapper acceptable. Do NOT create one class merely because every mapping must have a class.
