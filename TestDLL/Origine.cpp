extern "C"
{
#include <cplex.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <math.h>
#include <cut.h>
#include <allocrus.c>
#include <cut_st.c>
#include <macrorus.h>
#include <mincut.c>
#include <sortrus.c>
#include <util.h>


	typedef struct {
		int nnodes;
		double *xcoord;
		double *ycoord;

		// Global data							
		double zBest; // Value of the best solution available 
		double *bestSol; // Array of values of the variables of the best solution
		double tsol; // Time for calculating best solution
		int ncols; // Number of variables in the problem
		int nMaxCuts; // Number of cuts added in SECseparation function
		int *xpos; // Contains the number of variables x[i][j] representing an edge in the graph from CPLEX point of view
		char inputFile[1000]; // Input file
		double tlim; // Maximum time for CPLEX to solve an instance

	}instance;

	typedef struct { // Structure used in concorde function CCcut_violated_cuts() as input

		instance *inst;
		CPXCENVptr env;
		void *cbdata;
		int wherefrom;
		int *useraction_p;
	} input;
	void Concorde(char* nomeFile, int timeLimit);
	int CPXPUBLIC fractcutusercallback(CPXCENVptr env, void *cbdata, int wherefrom, void *cbhandle, int *useraction_p);
	int doitFuncConcorde(double cutValue, int cutcount, int *cut, void *inParam);
	void readInput(instance *inst);
	void buildModel(CPXENVptr env, CPXLPptr lp, instance *inst);
	double dist(instance *inst, int i, int j);
	void freeInstance(instance *inst);
	void myFree(void *pointer);
	void printError(const char *err);

	__declspec(dllexport) void DisplayHelloFromDLL(char* test, int i)
	{
		Concorde(test, i);
	}

	void Concorde(char* nomeFile, int timeLimit) {
		instance *inst;
		// Initializing parameters and pointers to their default values
		strcpy(inst->inputFile, nomeFile);
		inst->nnodes = 0;
		inst->tlim = timeLimit;
		inst->xcoord = NULL;
		inst->ycoord = NULL;
		inst->bestSol = NULL;
		inst->xpos = NULL;

		//Leggo l'input
		readInput(inst);

		int error;

		CPXENVptr env = CPXopenCPLEX(&error);
		CPXLPptr lp = CPXcreateprob(env, &error, "TSP");

		buildModel(env, lp, inst);
		inst->ncols = CPXgetnumcols(env, lp);

		inst->bestSol = (double*)calloc(inst->ncols, sizeof(double));
		inst->zBest = CPX_INFBOUND;

		//installo la callback
		CPXsetusercutcallbackfunc(env, fractcutusercallback, inst);

		CPXmipopt(env, lp);//risolvo modello	
	}

	/**
	* Callback that adds new cuts using the concorde library based on the connected components of the fractional solution
	*/
	int CPXPUBLIC fractcutusercallback(CPXCENVptr env, void *cbdata, int wherefrom, void *cbhandle, int *useraction_p) {

		CPXINT nodeDepth;
		CPXgetcallbacknodeinfo(env, cbdata, wherefrom, 0, CPX_CALLBACK_INFO_NODE_DEPTH, &nodeDepth);
		if (nodeDepth < 10) {
			*useraction_p = CPX_CALLBACK_DEFAULT;
			instance *inst = (instance*)cbhandle; // cbhandle for us is a pointer to a instance type object

												  // Allocating variables
			double *xstar = (double*)malloc(inst->ncols * sizeof(double)); // Vector of current optimal solution
			int *compscount = (int*)malloc(inst->nMaxCuts * sizeof(int)); // The number of nodes for each connected component
			int *comps = (int*)malloc(inst->nnodes * sizeof(int)); // Vector containing the nodes in the components
			int ncomp; // Number of connected components

					   // Getting value of local optimum
			if (CPXgetcallbacknodex(env, cbdata, wherefrom, xstar, 0, inst->ncols - 1)) {
				// Free memory
				free(comps);
				free(compscount);
				free(xstar);

				return 1; // In case the function returns an error
			}

			// Allocating edges list "elist" for function CCcut_connect_components
			int *elist = (int*)malloc(inst->ncols * 2 * sizeof(int));
			int eInd = 0;
			for (int i = 0; i < inst->nnodes - 1; ++i) {
				for (int j = i + 1; j < inst->nnodes; ++j) {
					elist[eInd++] = i;
					elist[eInd++] = j;
				}
			}

			// Finding out if the graph is connected
			if (CCcut_connect_components(inst->nnodes, inst->ncols, elist, xstar, &ncomp, &compscount, &comps))
				printError(" error in CCcut_connect_components() inside fractcutusercallback");

			// If the graph is not connected we add one cut for each connected component		
			if (ncomp > 1) {

				// Adding cuts for each connected component
				int pos = 0;
				for (int i = 0; i < ncomp; ++i) {


					int dimIndexValue = compscount[i] * (compscount[i] - 1) / 2 + 1; // Maximum dimension of index vector
					int *cutind = (int*)malloc(dimIndexValue * sizeof(int));
					double *cutval = (double*)malloc(dimIndexValue * sizeof(double));

					int k = 0;
					int k1 = pos;
					int k2 = k1 + compscount[i];
					pos = pos + compscount[i];
					for (int j = k1; j < k2 - 1; ++j) {
						for (int z = j + 1; z < k2; ++z) {
							cutval[k] = 1.0;
							cutind[k] = inst->xpos[comps[j] * inst->nnodes + comps[z]];
							k++;
						}
					}

					CPXcutcallbackadd(env, cbdata, wherefrom, k, compscount[i] - 1, 'L', cutind, cutval, CPX_USECUT_FORCE);
					

					*useraction_p = CPX_CALLBACK_SET; // Tells CPLEX that cuts have been created
					free(cutind);
					free(cutval);
				}

				// Free memory
				free(elist);
				free(comps);
				free(compscount);
				free(xstar);

				return 0;
			}

			input in;
			in.inst = inst;
			in.env = env;
			in.cbdata = cbdata;
			in.wherefrom = wherefrom;
			in.useraction_p = useraction_p;

			double cutThreshold = 0.1;

			// We consider a cut violated when the difference is greater than 0.1
			if (CCcut_violated_cuts(inst->nnodes, inst->ncols, elist, xstar, 2.0 - cutThreshold, doitFuncConcorde, (void*)&in) != 0) {
				printError(" error in CCcut_violated_cuts() inside fractcutusercallback");
			}

			// Free memory
			free(elist);
			free(comps);
			free(compscount);
			free(xstar);
		}

		return 0;
	}

	/**
	* Function called by the function CCcut_violated_cuts() inside fractcutusercallback() when it encounters a cut with capacity less than a certain value
	* Parameters: cutValue = capacity of cut; cutcount = number of nodes in cut; *cut = list of nodes in cut
	*/
	int doitFuncConcorde(double cutValue, int cutcount, int *cut, void *inParam) {

		input *in = (input*)inParam;
		instance *inst = in->inst;

		int dimIndexValue = inst->nnodes * (inst->nnodes - 1) / 2 + 1; // Maximum dimension of index vector
		int *cutind = (int*)malloc(dimIndexValue * sizeof(int));
		double *cutval = (double*)malloc(dimIndexValue * sizeof(double));

		int nnz = 0;
		for (int i = 0; i < cutcount - 1; ++i) {
			for (int j = i + 1; j < cutcount; ++j) {
				cutind[nnz] = inst->xpos[cut[i] * inst->nnodes + cut[j]];
				cutval[nnz++] = 1.0;
			}
		}

		CPXcutcallbackadd(in->env, in->cbdata, in->wherefrom, nnz, cutcount - 1, 'L', cutind, cutval, CPX_USECUT_FORCE);

		*in->useraction_p = CPX_CALLBACK_SET; // Tells CPLEX that cuts have been created

		free(cutval);
		free(cutind);

		return 0;
	}


	/**
	* Reads the problem data from an input file in the TSPLIB library format
	*/
	void readInput(instance *inst) {

		FILE *fin = fopen(inst->inputFile, "r");

		if (fin == NULL)
			//printError(" input file not found!");

			inst->nnodes = -1;

		char line[180];
		char *parName;
		char *token1;
		char *token2;

		int activeSection = 0; // =1 NODE_COORD_SECTION


		while (fgets(line, sizeof(line), fin) != NULL) {


			if (strlen(line) <= 1)
				continue; // skip empty lines

			parName = strtok(line, " :");


			if (strncmp(parName, "NAME", 4) == 0) {
				activeSection = 0;
				continue;
			}

			if (strncmp(parName, "COMMENT", 7) == 0) {
				activeSection = 0;
				token1 = strtok(NULL, "");
				continue;
			}

			if (strncmp(parName, "TYPE", 4) == 0) {
				token1 = strtok(NULL, " :");
				if (strncmp(token1, "TSP", 3) != 0)
					//printError(" format error:  only TYPE == TSP implemented so far!!!!!!"); 
					activeSection = 0;
				continue;
			}


			if (strncmp(parName, "DIMENSION", 9) == 0) {
				if (inst->nnodes >= 0)
					//printError(" repeated DIMENSION section in input file");

					token1 = strtok(NULL, " :");
				inst->nnodes = atoi(token1);

				inst->xcoord = (double*)malloc(inst->nnodes * sizeof(double));
				inst->ycoord = (double*)malloc(inst->nnodes * sizeof(double));
				inst->xpos = (int*)malloc(inst->nnodes * inst->nnodes * sizeof(int));
				activeSection = 0;
				continue;
			}


			if (strncmp(parName, "EDGE_WEIGHT_TYPE", 16) == 0) {
				token1 = strtok(NULL, " :");
				if (strncmp(token1, "EUC_2D", 6) != 0)
					//printError(" format error:  only EDGE_WEIGHT_TYPE == EUC_2D implemented so far!!!!!!");

					activeSection = 0;
				continue;
			}

			if (strncmp(parName, "NODE_COORD_SECTION", 18) == 0) {
				if (inst->nnodes <= 0)
					//printError(" ... DIMENSION section should appear before NODE_COORD_SECTION section");

					activeSection = 1;
				continue;
			}

			if (strncmp(parName, "EOF", 3) == 0) {
				activeSection = 0;
				break;
			}

			// Within NODE_COORD_SECTION
			if (activeSection == 1) {
				int i = atoi(parName) - 1;
				if (i < 0 || i >= inst->nnodes)
					//printError(" ... unknown node in NODE_COORD_SECTION section");     
					token1 = strtok(NULL, " :,");
				token2 = strtok(NULL, " :,");
				inst->xcoord[i] = atof(token1);
				inst->ycoord[i] = atof(token2);
				continue;
			}

			printf(" final active section %d\n", activeSection);
			//printError(" ... wrong format for the current simplified parser!!!!!!!!!");     

		}

		fclose(fin);

	}

	void buildModel(CPXENVptr env, CPXLPptr lp, instance *inst) {

		double lb = 0.0; // variables lower bound, upper bound is: up = 1.0; 	
		char type = 'B'; // Variable type: binary
		// Other types of variables
		//char continuous = 'C';
		//char integer = 'I';

		char **cname = (char **)calloc(1, sizeof(char *)); // (char **) is required by CPLEX...
		cname[0] = (char *)calloc(100, sizeof(char));

		for (int i = 0; i < inst->nnodes - 1; ++i) { // Adding new columns (variables) to CPLEX table
			inst->xpos[i*inst->nnodes + i] = -1;

			for (int j = i + 1; j < inst->nnodes; ++j) {
				sprintf(cname[0], "x(%d,%d)", i + 1, j + 1); // Prints inside the string cname[0]
				double obj = dist(inst, i, j);
				double ub = (i == j) ? 0.0 : 1.0;
				if (CPXnewcols(env, lp, 1, &obj, &lb, &ub, &type, cname)) // Inserting new column (variable) in the table
					printError(" problem in CPXnewcols in buildModel");

				inst->xpos[i*inst->nnodes + j] = inst->xpos[j*inst->nnodes + i] = CPXgetnumcols(env, lp) - 1; // xpos[i][j] has the position in CPLEX table of variable X(i, j)
			}
		}

		double rhs = 2.0; // Right hand side term of the constraint
		char sense = 'E'; // Equation

		for (int v = 0; v < inst->nnodes; ++v) { // Adding new rows (constraints) to CPLEX table
			sprintf(cname[0], "degree(%d)", v + 1);

			if (CPXnewrows(env, lp, 1, &rhs, &sense, NULL, cname)) // Adding an empty row
				printError(" error in CPXnewrows in buildModel");

			int lastrow = CPXgetnumrows(env, lp) - 1;

			for (int i = 0; i < inst->nnodes; ++i) { // Filling empty row
				if (i == v) continue;

				int pos = inst->xpos[v*inst->nnodes + i];
				if (CPXchgcoef(env, lp, lastrow, pos, 1.0))
					printError(" error in CPXchgcoef in buildModel");
			}
		}

		free(cname[0]);
		free(cname);
	}

	double dist(instance *inst, int i, int j) {

		double dx = inst->xcoord[i] - inst->xcoord[j];
		double dy = inst->ycoord[i] - inst->ycoord[j];

		int dis = sqrt(dx*dx + dy*dy) + 0.499999999; // Nearest integer 
		return dis + 0.0;
	}

	/**
	* Frees pointers inside *inst
	*/
	void freeInstance(instance *inst) {
		myFree(inst->xcoord);
		myFree(inst->ycoord);
		myFree(inst->bestSol);
		myFree(inst->xpos);
	}

	/**
	* Frees a pointer only if it's not NULL
	*/
	void myFree(void *pointer) {
		if (pointer != NULL) {
			free(pointer);
		}
	}

	/**
	* Prints a message on screen and terminates the executing program
	*/
	void printError(const char *err) {
		printf("\n\n ERROR: %s \n\n", err);
		fflush(NULL);
		exit(1);
	}
}
