# GRAL Dispersion Model<br>
Atmospheric dispersion modeling in complex terrain and in situations with low wind speeds is still challenging. Nevertheless, air pollution has to be assessed in such environments.
It is therefore necessary to develop models and methods, which allow for such assessments with reasonable demands on computational times and with sensible accuracy.
This has been the motivation for the development of the Lagrangian dispersion model GRAL at the Graz University of Technology, Institute for Internal Combustion Engines and Thermodynamics, ever since 1999. <br>
Dr. Dietmar Oettl from the Office of the Provincial Government of Styria (2006 - 2020) and Markus Kuntner (since 2016), Austria, are further developing the model. In 2019, the decision was made to publish GRAL as Open Source.<br>

The basic principle of Lagrangian models is the tracing/tracking of a multitude of fictitious particles moving on trajectories within a 3-d windfield. GRAL provides a CFD model for the flow calculation around buildings or micrsocale terrain structures. To take the presence of topography into account, GRAL can be linked with the prognostic wind field model GRAMM.<br>
To speed up the calculation, GRAL is parallelized, the CFD model supports SSE/AVX vectorization and the latest performance optimizations of the .NET framework are used.<br>

## Validation
GRAL has been used to calculate a large number of validation data sets, which are continuously updated and documented in the GRAL manual. The manual is available on the GRAL homepage in the download section. Individual validation projects are also published on Github.<br>

## Usage
It is possible to create or evaluate all input files and output files, which are text files and/or binary files, by yourself. The file formats are documented in the GRAL manual. <br>
We are also developing a comprehensive graphical user interface (GUI), in order to simplify the numerous input values, display the results and allow verification of your model input and output. Our goal is to provide the user with a simple and comprehensive model checking tool to support the quality requirements of dispersion calculations. Also the GUI is free and completely published as Open Source.<br>

## Built With
* [Visual Studio 2019](https://visualstudio.microsoft.com/de/downloads/) 
* [Visual Studio Code](https://code.visualstudio.com/)

## Official Release and Documentation
The current validated and signed GRAL version, the documentation and a recommendation guide is available at the [GRAL homepage](https://gral.tugraz.at/)

## Contributing
Everyone is invited to contribute to the project [Contributing](Contributing.md)
 
## Versioning
The version number includes the release year and the release month, e.g. 20.01.

## License
This project is licensed under the GPL 3.0 License - see the [License](License.md) file for details
