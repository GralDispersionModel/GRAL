# GRAL Dispersion Model<br>
Dispersion modelling in complex terrain and in situations with low wind speeds is still challenging. Nevertheless, air pollution has to be assessed in such environments.
It is therefore necessary to develop models and methods, which allow for such assessments with reasonable demands on computational times and with sensible accuracy.
This has been the motivation for the development of the Lagrangian dispersion model GRAL at the Graz University of Technology, Institute for Internal Combustion Engines and Thermodynamics, ever since 1999. <br>
Meanwhile the Governments of Tyrol (since 2014) and Styria (since 2006), Austria, are further developing the model. In 2019, the decision was made to publish GRAL as Open Source.<br>

The basic principle of Lagrangian models is the tracing/tracking of a multitude of fictitious particles moving on trajectories within a 3-d windfield. GRAL provides a CFD model for the flow calculation around buildings or micrsocale terrain structures. To take the presence of topography into account, GRAL can be linked with the prognostic wind field model GRAMM.<br>
To speed up the calculation, GRAL is parallelized, the CFD model supports SSE/AVX vectorization.<br>

We developed a powerful graphical user interface (GUI) to simplify the numerous input values, to display the results, and to enable checks of your model input and output. Also the GUI is free and completely published as Open Source.<br>

## Built With
* [Visual Studio 2019](https://visualstudio.microsoft.com/de/downloads/) 
* [Visual Studio Code](https://code.visualstudio.com/)

## Official Release and Documentation
The current validated and signed GRAL version, the documentation and a recommendation guide is available at the [GRAL homepage](http://lampz.tugraz.at/~gral/)

## Contributing
Everyone is invited to contribute to the project [Contributing](Contributing.md)
 
## Versioning
The version number includes the release year and the release month, e.g. 20.01.

## License
This project is licensed under the GPL 3.0 License - see the [License](License.md) file for details
